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
