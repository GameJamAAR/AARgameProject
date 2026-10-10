#ifndef AAR_RETRO_SCREEN_CORE_INCLUDED
#define AAR_RETRO_SCREEN_CORE_INCLUDED

struct RetroScreenSettings
{
    float2 curvature;
    float zoom;
    float cornerRadius;
    float borderWidth;
    float edgeFeather;
};

// Inverse mapping: each output pixel samples a bent position in the source image.
float2 RetroScreenWarp(float2 uv, RetroScreenSettings settings)
{
    float2 centered = (uv * 2.0 - 1.0) / max(settings.zoom, 0.01);
    // Radial barrel distortion bends the sampled IMAGE in both axes, including
    // objects near the middle of an edge, rather than just rounding the bezel.
    float radiusSquared = dot(centered, centered);
    return centered * (1.0 + settings.curvature * radiusSquared) * 0.5 + 0.5;
}

// Solve source = p * (1 + curvature * |p|²). Its scalar radius equation
// is monotone, so Newton iteration is stable even at the strongest allowed bend.
float2 RetroScreenUnwarp(float2 sourceUV, float2 curvature, float zoom)
{
    float2 source = sourceUV * 2.0 - 1.0;
    float2 squared = source * source;
    float radius = dot(source, source);
    [unroll] for (int iteration = 0; iteration < 6; ++iteration)
    {
        float2 divisor = 1.0 + curvature * radius;
        float2 divisor2 = divisor * divisor;
        float residual = radius - dot(squared / divisor2, float2(1,1));
        float derivative = 1.0 + 2.0 * dot(curvature * squared / (divisor2 * divisor), float2(1,1));
        radius = max(radius - residual / derivative, 0.0);
    }
    return source / (1.0 + curvature * radius) * max(zoom, 0.01) * 0.5 + 0.5;
}

float RetroScreenScanline(float2 warpedUV, float renderedHeight, float requestedCount,
    float width, RetroScreenSettings settings)
{
    // Leave at least three output pixels per cycle even at the most stretched edge.
    // A single global cap avoids spatially varying frequencies and moire swirls.
    float stretch = (1.0 + 6.0 * max(settings.curvature.x, settings.curvature.y)
        / (settings.zoom * settings.zoom)) / max(settings.zoom, 0.01);
    float count = min(requestedCount, max(1.0, renderedHeight / (3.0 * stretch)));
    float phase = warpedUV.y * count;
    float antialias = max(fwidth(phase) * 0.5, 0.0001);
    float distanceToLine = abs(frac(phase) - 0.5);
    return 1.0 - smoothstep(width * 0.5 - antialias, width * 0.5 + antialias, distanceToLine);
}

float RetroScreenMask(float2 warpedUV, float aspect, RetroScreenSettings settings)
{
    float2 halfSize = float2(aspect, 1.0) * (1.0 - settings.borderWidth);
    float radius = min(settings.cornerRadius, min(halfSize.x, halfSize.y));
    float2 p = (warpedUV * 2.0 - 1.0) * float2(aspect, 1.0);
    float2 q = abs(p) - halfSize + radius;
    float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    float feather = max(settings.edgeFeather, 0.00001);
    return 1.0 - smoothstep(-feather, feather, distance);
}

#endif
