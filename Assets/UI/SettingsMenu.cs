using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Menu Paramètres ouvert avec l'action "Pause" (Échap / Start).
///  - Onglet Commandes : toutes les touches du joueur, modifiables (clavier/souris et manette),
///    avec échange automatique en cas de conflit et sauvegarde entre deux parties.
///  - Onglet Jeu : vue 1re/3e personne, sensibilités, axe inversé, accroupi, champ de vision.
/// L'interface est construite par code (uGUI + TextMeshPro) : aucun prefab à placer.
/// Créé automatiquement par PlayerController.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    // ---------- Palette ----------
    private static readonly Color Overlay   = new Color(0.02f, 0.03f, 0.04f, 0.72f);
    private static readonly Color PanelCol  = new Color32(14, 17, 22, 245);
    private static readonly Color Surface   = new Color32(24, 29, 37, 255);
    private static readonly Color SurfaceHi = new Color32(34, 41, 52, 255);
    private static readonly Color SurfacePr = new Color32(44, 53, 66, 255);
    private static readonly Color Accent    = new Color32(60, 242, 163, 255);
    private static readonly Color AccentDim = new Color32(60, 242, 163, 40);
    private static readonly Color TextCol   = new Color32(230, 237, 243, 255);
    private static readonly Color Muted     = new Color32(139, 148, 158, 255);
    private static readonly Color Danger    = new Color32(255, 107, 107, 255);

    private const string KeyboardGroup = "Keyboard&Mouse";
    private const string GamepadGroup = "Gamepad";

    // Noms affichés des actions
    private static readonly Dictionary<string, string> ActionLabels = new Dictionary<string, string>
    {
        { "Jump", "Sauter" }, { "Crouch", "S'accroupir" }, { "Sprint", "Courir" },
        { "Attack", "Attaquer" }, { "Interact", "Interagir" },
        { "Previous", "Précédent" }, { "Next", "Suivant" },
        { "ToggleView", "Vue 1re / 3e personne" }, { "Pause", "Menu / Pause" },
    };
    private static readonly Dictionary<string, string> MoveLabels = new Dictionary<string, string>
    {
        { "up", "Avancer" }, { "down", "Reculer" }, { "left", "Aller à gauche" }, { "right", "Aller à droite" },
    };

    // ---------- État ----------
    private static SettingsMenu instance;
    public static bool IsOpen => instance != null && instance.isOpen;

    private PlayerController player;
    private bool isOpen;
    private float previousTimeScale = 1f;
    private bool blockPauseThisFrame;

    private GameObject root;
    private GameObject commandsPage, gamePage;
    private Button commandsTab, gameTab, resumeButton;
    private TextMeshProUGUI statusText;
    private readonly List<BindingCell> cells = new List<BindingCell>();
    private InputActionRebindingExtensions.RebindingOperation rebindOp;
    private Action refreshGamePage;

    private static Sprite rounded, circle;

    private class BindingCell
    {
        public InputAction action;
        public int bindingIndex;
        public string group;
        public string rowLabel;
        public Button button;
        public TextMeshProUGUI text;
        public Image background;
    }

    /// <summary>Crée le menu s'il n'existe pas et le relie au joueur.</summary>
    public static void Ensure(PlayerController p)
    {
        if (instance == null)
        {
            var go = new GameObject("SettingsMenu");
            instance = go.AddComponent<SettingsMenu>();
        }
        instance.player = p;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        EnsureEventSystem();
        BuildUI();
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        rebindOp?.Dispose();
        if (instance == this)
        {
            instance = null;
            if (isOpen) Time.timeScale = previousTimeScale;
        }
    }

    private void Update()
    {
        if (player == null || rebindOp != null) return;
        if (blockPauseThisFrame) { blockPauseThisFrame = false; return; }

        var pause = player.PauseAction;
        if (pause != null && pause.WasPressedThisFrame())
        {
            if (isOpen) Close(); else Open();
        }
    }

    // ==================================================================
    public void Open()
    {
        if (isOpen || player == null) return;
        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        player.SetInputEnabled(false);
        RefreshBindings();
        refreshGamePage?.Invoke();
        SetStatus("Clique sur une touche pour la modifier.  Échap annule.", Muted);
        root.SetActive(true);
        ShowPage(commandsPage.activeSelf);
        EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Close()
    {
        if (!isOpen) return;
        CancelRebind();
        isOpen = false;
        root.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (player != null) player.SetInputEnabled(true);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    // ==================================================================
    // Construction de l'interface
    // ==================================================================
    private void BuildUI()
    {
        rounded = MakeRoundedSprite(64, 14);
        circle = MakeRoundedSprite(64, 32);

        var canvasGO = new GameObject("Canvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        root = canvasGO;

        // Fond assombri (bloque les clics vers le jeu)
        var overlay = Rect("Overlay", canvasGO.transform);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = Overlay;

        // Panneau principal
        var panel = Rect("Panel", canvasGO.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1040, 760);
        AddImage(panel, PanelCol, rounded);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 36, 32);
        v.spacing = 18;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        // Liseré d'accent en haut
        var accentBar = Rect("AccentBar", panel);
        accentBar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        accentBar.anchorMin = new Vector2(0, 1); accentBar.anchorMax = new Vector2(1, 1);
        accentBar.pivot = new Vector2(0.5f, 1);
        accentBar.offsetMin = new Vector2(40, -4); accentBar.offsetMax = new Vector2(-40, 0);
        AddImage(accentBar, Accent, rounded);

        // En-tête
        var header = Row("Header", panel, 48, 12);
        var title = Label(header, "PARAMÈTRES", 38, TextCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        title.characterSpacing = 6;
        Flexible(title.gameObject);
        Label(header, "Échap pour reprendre", 18, Muted, TextAlignmentOptions.MidlineRight).gameObject
            .AddComponent<LayoutElement>().preferredWidth = 260;

        // Onglets
        var tabs = Row("Tabs", panel, 46, 8);
        commandsTab = MakeButton(tabs, "Commandes", () => ShowPage(true), false, 200);
        gameTab = MakeButton(tabs, "Jeu", () => ShowPage(false), false, 200);
        Flexible(Rect("Spacer", tabs).gameObject);

        // Contenu
        var content = Rect("Content", panel);
        var contentLE = content.gameObject.AddComponent<LayoutElement>();
        contentLE.flexibleHeight = 1;
        contentLE.minHeight = 300;

        commandsPage = BuildCommandsPage(content);
        gamePage = BuildGamePage(content);

        // Ligne d'état
        statusText = Label(panel, "", 17, Muted, TextAlignmentOptions.MidlineLeft);
        statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        // Pied de page
        var footer = Row("Footer", panel, 52, 12);
        MakeButton(footer, "Réinitialiser les touches", ResetBindings, false, 280);
        Flexible(Rect("Spacer", footer).gameObject);
        MakeButton(footer, "Quitter le jeu", QuitGame, false, 200);
        resumeButton = MakeButton(footer, "Reprendre", Close, true, 200);

        ShowPage(true);
    }

    private GameObject BuildCommandsPage(RectTransform parent)
    {
        var page = Rect("CommandsPage", parent);
        Stretch(page);
        var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        // En-têtes de colonnes
        var head = Row("ColumnHeader", page, 28, 12);
        head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(20, 20, 0, 0);
        Flexible(Label(head, "ACTION", 15, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).gameObject);
        Fixed(Label(head, "CLAVIER / SOURIS", 15, Muted, TextAlignmentOptions.Center, FontStyles.Bold).gameObject, 260);
        Fixed(Label(head, "MANETTE", 15, Muted, TextAlignmentOptions.Center, FontStyles.Bold).gameObject, 260);

        // Zone défilante
        var scroll = Rect("Scroll", page);
        scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30;

        var viewport = Rect("Viewport", scroll);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear; // reçoit la molette

        var list = Rect("List", viewport);
        list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1);
        list.pivot = new Vector2(0.5f, 1);
        list.offsetMin = list.offsetMax = Vector2.zero;
        var lv = list.gameObject.AddComponent<VerticalLayoutGroup>();
        lv.spacing = 8;
        lv.padding = new RectOffset(0, 8, 0, 4);
        lv.childControlWidth = lv.childControlHeight = true;
        lv.childForceExpandWidth = true;
        lv.childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content = list;

        BuildBindingRows(list);
        return page.gameObject;
    }

    private void BuildBindingRows(RectTransform list)
    {
        cells.Clear();
        var map = player != null ? player.PlayerMap : null;
        if (map == null) { Label(list, "Aucune commande trouvée.", 20, Muted, TextAlignmentOptions.Center); return; }

        foreach (var action in map.actions)
        {
            if (action.name == "Look") continue; // la souris / le stick droit ne se réassignent pas

            if (action.name == "Move")
            {
                int gp = FindBinding(action, GamepadGroup, false);
                bool first = true;
                foreach (int part in FindCompositeParts(action, KeyboardGroup))
                {
                    string partName = action.bindings[part].name;
                    string label = MoveLabels.TryGetValue(partName, out var l) ? l : partName;
                    BindingRow(list, label, action, part, first ? gp : -1);
                    first = false;
                }
                continue;
            }

            string name = ActionLabels.TryGetValue(action.name, out var n) ? n : action.name;
            BindingRow(list, name, action, FindBinding(action, KeyboardGroup, true), FindBinding(action, GamepadGroup, false));
        }
    }

    private void BindingRow(RectTransform list, string label, InputAction action, int kbIndex, int gpIndex)
    {
        var row = Row(label, list, 56, 12);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(20, 8, 6, 6);
        AddImage(row, Surface, rounded);
        Flexible(Label(row, label, 21, TextCol, TextAlignmentOptions.MidlineLeft).gameObject);
        BindingButton(row, label, action, kbIndex, KeyboardGroup);
        BindingButton(row, label, action, gpIndex, GamepadGroup);
    }

    private void BindingButton(RectTransform row, string rowLabel, InputAction action, int index, string group)
    {
        var cell = new BindingCell { action = action, bindingIndex = index, group = group, rowLabel = rowLabel };
        cell.button = MakeButton(row, "", () => StartRebind(cell), false, 260);
        cell.text = cell.button.GetComponentInChildren<TextMeshProUGUI>();
        cell.background = cell.button.GetComponent<Image>();
        if (index < 0)
        {
            cell.button.interactable = false;
            cell.text.text = "—";
            cell.text.color = Muted;
        }
        cells.Add(cell);
    }

    private GameObject BuildGamePage(RectTransform parent)
    {
        var page = Rect("GamePage", parent);
        Stretch(page);
        var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var view = Segmented(SettingRow(page, "Vue"), new[] { "1re personne", "3e personne" },
            i => player.SetViewMode(i == 0 ? PlayerController.ViewMode.FirstPerson : PlayerController.ViewMode.ThirdPerson));
        var mouse = SliderControl(SettingRow(page, "Sensibilité souris"), 0.02f, 0.5f, "0.00",
            x => player.MouseSensitivity = x);
        var pad = SliderControl(SettingRow(page, "Sensibilité manette"), 30f, 400f, "0",
            x => player.GamepadSensitivity = x);
        var invert = Switch(SettingRow(page, "Inverser l'axe vertical"), b => player.InvertY = b);
        var crouch = Segmented(SettingRow(page, "Accroupi"), new[] { "Maintenir", "Basculer" },
            i => player.CrouchToggle = i == 1);
        var fov = SliderControl(SettingRow(page, "Champ de vision"), 50f, 100f, "0°",
            x => player.FieldOfView = x);

        refreshGamePage = () =>
        {
            if (player == null) return;
            view(player.CurrentViewMode == PlayerController.ViewMode.FirstPerson ? 0 : 1);
            mouse(player.MouseSensitivity);
            pad(player.GamepadSensitivity);
            invert(player.InvertY);
            crouch(player.CrouchToggle ? 1 : 0);
            fov(player.FieldOfView);
        };
        return page.gameObject;
    }

    private void ShowPage(bool commands)
    {
        commandsPage.SetActive(commands);
        gamePage.SetActive(!commands);
        StyleTab(commandsTab, commands);
        StyleTab(gameTab, !commands);
    }

    private static void StyleTab(Button tab, bool active)
    {
        var colors = tab.colors;
        colors.normalColor = active ? AccentDim : Color.clear;
        colors.highlightedColor = active ? AccentDim : SurfaceHi;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = SurfacePr;
        tab.colors = colors;
        var t = tab.GetComponentInChildren<TextMeshProUGUI>();
        t.color = active ? Accent : Muted;
        t.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
    }

    // ==================================================================
    // Réassignation des touches
    // ==================================================================
    private void StartRebind(BindingCell cell)
    {
        if (rebindOp != null || cell.bindingIndex < 0) return;

        var action = cell.action;
        string oldPath = action.bindings[cell.bindingIndex].effectivePath;
        bool wasEnabled = action.enabled;
        action.Disable();
        player.PauseAction?.Disable(); // Échap sert à annuler

        cell.text.text = "Appuie sur une touche…";
        cell.text.color = Accent;
        SetStatus($"« {cell.rowLabel} » : appuie sur la nouvelle touche ({(cell.group == GamepadGroup ? "manette" : "clavier ou souris")}). Échap annule.", Accent);

        var op = action.PerformInteractiveRebinding(cell.bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .OnMatchWaitForAnother(0.05f);

        if (cell.group == GamepadGroup) op.WithControlsHavingToMatchPath("<Gamepad>");
        else op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");

        op.OnComplete(o =>
            {
                string swapped = ResolveConflict(action, cell.bindingIndex, oldPath);
                FinishRebind(action, wasEnabled);
                GameSettings.SaveBindings(player.PlayerMap);
                SetStatus(swapped != null
                    ? $"Touche échangée avec « {swapped} »."
                    : $"« {cell.rowLabel} » : {action.GetBindingDisplayString(cell.bindingIndex)}", swapped != null ? Accent : Muted);
            })
            .OnCancel(o =>
            {
                FinishRebind(action, wasEnabled);
                SetStatus("Modification annulée.", Muted);
            });

        rebindOp = op;
        op.Start();
    }

    private void FinishRebind(InputAction action, bool wasEnabled)
    {
        rebindOp?.Dispose();
        rebindOp = null;
        if (wasEnabled) action.Enable();
        RefreshBindings();
        StartCoroutine(ReenablePauseNextFrame());
    }

    // Évite qu'Échap (qui vient d'annuler) referme aussi le menu
    private IEnumerator ReenablePauseNextFrame()
    {
        blockPauseThisFrame = true;
        yield return null;
        if (isOpen) player?.PauseAction?.Enable();
    }

    private void CancelRebind()
    {
        if (rebindOp != null) rebindOp.Cancel();
    }

    /// <summary>Si la nouvelle touche est déjà utilisée ailleurs, on donne l'ancienne touche à l'autre action.</summary>
    private string ResolveConflict(InputAction action, int index, string oldPath)
    {
        string newPath = action.bindings[index].effectivePath;
        if (string.IsNullOrEmpty(newPath) || newPath == oldPath) return null;

        foreach (var cell in cells)
        {
            if (cell.bindingIndex < 0 || (cell.action == action && cell.bindingIndex == index)) continue;
            var b = cell.action.bindings[cell.bindingIndex];
            if (string.Equals(b.effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                cell.action.ApplyBindingOverride(cell.bindingIndex, oldPath);
                return cell.rowLabel;
            }
        }
        return null;
    }

    private void ResetBindings()
    {
        CancelRebind();
        player?.PlayerMap?.RemoveAllBindingOverrides();
        GameSettings.SaveBindings(player?.PlayerMap);
        RefreshBindings();
        SetStatus("Touches par défaut rétablies.", Accent);
    }

    private void RefreshBindings()
    {
        // Le menu a pu être construit avant que le joueur n'existe
        if (cells.Count == 0 && player != null && player.PlayerMap != null)
        {
            var list = commandsPage.GetComponentInChildren<ScrollRect>(true).content;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            BuildBindingRows(list);
        }

        // Repère les touches en double
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cells)
        {
            if (c.bindingIndex < 0) continue;
            string p = c.action.bindings[c.bindingIndex].effectivePath;
            if (!string.IsNullOrEmpty(p)) counts[p] = counts.TryGetValue(p, out int k) ? k + 1 : 1;
        }

        foreach (var c in cells)
        {
            if (c.bindingIndex < 0) continue;
            string display = c.action.GetBindingDisplayString(c.bindingIndex);
            c.text.text = string.IsNullOrEmpty(display) ? "Non assignée" : display;
            string p = c.action.bindings[c.bindingIndex].effectivePath;
            bool duplicate = !string.IsNullOrEmpty(p) && counts.TryGetValue(p, out int n) && n > 1;
            c.text.color = duplicate ? Danger : TextCol;
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (statusText == null) return;
        statusText.text = text;
        statusText.color = color;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================================================================
    // Recherche des bindings
    // ==================================================================
    private static int FindBinding(InputAction action, string group, bool keyboard)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (HasGroup(b, group)) return i;
        }
        // Map créée par code (sans groupes) : on devine par le chemin
        string prefix = keyboard ? "<Keyboard>" : "<Gamepad>";
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (b.path.StartsWith(prefix) || (keyboard && b.path.StartsWith("<Mouse>"))) return i;
        }
        return -1;
    }

    private static List<int> FindCompositeParts(InputAction action, string group)
    {
        var parts = new List<int>();
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            if (!bindings[i].isComposite) continue;
            int j = i + 1;
            var candidate = new List<int>();
            while (j < bindings.Count && bindings[j].isPartOfComposite) candidate.Add(j++);
            if (candidate.Count > 0 && (HasGroup(bindings[candidate[0]], group) ||
                                        bindings[candidate[0]].path.StartsWith("<Keyboard>")))
                return candidate;
        }
        return parts;
    }

    private static bool HasGroup(InputBinding b, string group)
    {
        if (string.IsNullOrEmpty(b.groups)) return false;
        foreach (var g in b.groups.Split(';'))
            if (g == group) return true;
        return false;
    }

    // ==================================================================
    // Widgets
    // ==================================================================
    private RectTransform SettingRow(RectTransform page, string label)
    {
        var row = Row(label, page, 64, 16);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(22, 14, 8, 8);
        AddImage(row, Surface, rounded);
        Flexible(Label(row, label, 21, TextCol, TextAlignmentOptions.MidlineLeft).gameObject);
        var control = Rect("Control", row);
        Fixed(control.gameObject, 420);
        return control;
    }

    /// <summary>Contrôle segmenté. Renvoie une fonction pour afficher la valeur sans déclencher l'action.</summary>
    private Action<int> Segmented(RectTransform parent, string[] options, Action<int> onChange)
    {
        var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;

        var buttons = new Button[options.Length];
        Action<int> show = index =>
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == index;
                var c = buttons[i].colors;
                c.normalColor = on ? Accent : SurfaceHi;
                c.highlightedColor = on ? Accent : SurfacePr;
                c.selectedColor = c.highlightedColor;
                c.pressedColor = on ? Accent : SurfacePr;
                buttons[i].colors = c;
                var t = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                t.color = on ? PanelCol : TextCol;
                t.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        };
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            buttons[i] = MakeButton(parent, options[i], () => { onChange(index); show(index); }, false, -1);
        }
        return show;
    }

    /// <summary>Interrupteur on/off. Renvoie une fonction pour afficher la valeur.</summary>
    private Action<bool> Switch(RectTransform parent, Action<bool> onChange)
    {
        bool value = false;
        var holder = Rect("Switch", parent);
        holder.anchorMin = holder.anchorMax = new Vector2(1, 0.5f);
        holder.pivot = new Vector2(1, 0.5f);
        holder.sizeDelta = new Vector2(64, 34);
        holder.anchoredPosition = Vector2.zero;
        var bg = AddImage(holder, SurfacePr, circle);
        var button = holder.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.colorMultiplier = 1.2f;
        button.colors = colors;

        var knob = Rect("Knob", holder);
        knob.anchorMin = knob.anchorMax = new Vector2(0, 0.5f);
        knob.sizeDelta = new Vector2(26, 26);
        var knobImg = AddImage(knob, TextCol, circle);
        knobImg.raycastTarget = false;

        Action<bool> show = on =>
        {
            value = on;
            bg.color = on ? Accent : SurfacePr;
            knob.anchorMin = knob.anchorMax = new Vector2(on ? 1 : 0, 0.5f);
            knob.anchoredPosition = new Vector2(on ? -17 : 17, 0);
            knobImg.color = on ? PanelCol : TextCol;
        };
        button.onClick.AddListener(() => { show(!value); onChange(value); });
        show(false);
        return show;
    }

    /// <summary>Curseur avec valeur affichée. Renvoie une fonction pour afficher la valeur.</summary>
    private Action<float> SliderControl(RectTransform parent, float min, float max, string format, Action<float> onChange)
    {
        var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        var sliderRT = Rect("Slider", parent);
        Flexible(sliderRT.gameObject);
        var slider = sliderRT.gameObject.AddComponent<Slider>();

        var track = Rect("Track", sliderRT);
        track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f);
        track.sizeDelta = new Vector2(0, 8);
        AddImage(track, SurfacePr, circle);

        var fillArea = Rect("FillArea", sliderRT);
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
        fillArea.sizeDelta = new Vector2(0, 8);
        var fill = Rect("Fill", fillArea);
        fill.sizeDelta = Vector2.zero;
        AddImage(fill, Accent, circle);

        var handleArea = Rect("HandleArea", sliderRT);
        handleArea.anchorMin = new Vector2(0, 0.5f); handleArea.anchorMax = new Vector2(1, 0.5f);
        handleArea.sizeDelta = new Vector2(-22, 22);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(22, 0);
        var handleImg = AddImage(handle, TextCol, circle);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        var colors = slider.colors;
        colors.highlightedColor = Accent;
        colors.pressedColor = Accent;
        colors.selectedColor = Accent;
        slider.colors = colors;

        var valueText = Label(parent, "", 19, Muted, TextAlignmentOptions.MidlineRight);
        Fixed(valueText.gameObject, 70);

        string Fmt(float x) => format.EndsWith("°") ? x.ToString(format.TrimEnd('°')) + "°" : x.ToString(format);
        slider.onValueChanged.AddListener(x => { valueText.text = Fmt(x); onChange(x); });
        return x => { slider.SetValueWithoutNotify(x); valueText.text = Fmt(x); };
    }

    private Button MakeButton(RectTransform parent, string text, UnityEngine.Events.UnityAction onClick, bool primary, float width)
    {
        var rt = Rect(text.Length > 0 ? text : "Button", parent);
        var img = AddImage(rt, Color.white, rounded);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var c = b.colors;
        c.normalColor = primary ? Accent : SurfaceHi;
        c.highlightedColor = primary ? new Color32(110, 255, 192, 255) : SurfacePr;
        c.selectedColor = c.highlightedColor;
        c.pressedColor = primary ? new Color32(40, 200, 135, 255) : Surface;
        c.disabledColor = new Color(1f, 1f, 1f, 0f);
        c.fadeDuration = 0.08f;
        b.colors = c;
        b.onClick.AddListener(onClick);

        var t = Label(rt, text, 20, primary ? PanelCol : TextCol, TextAlignmentOptions.Center,
            primary ? FontStyles.Bold : FontStyles.Normal);
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(12, 0);
        t.rectTransform.offsetMax = new Vector2(-12, 0);
        t.enableAutoSizing = true;
        t.fontSizeMin = 13;
        t.fontSizeMax = 20;

        if (width > 0) Fixed(rt.gameObject, width);
        else Flexible(rt.gameObject);
        return b;
    }

    // ---------- Aides de mise en page ----------
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Row(string name, Transform parent, float height, float spacing)
    {
        var rt = Rect(name, parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Flexible(GameObject go)
    {
        if (!go.TryGetComponent(out LayoutElement le)) le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
    }

    private static void Fixed(GameObject go, float width)
    {
        if (!go.TryGetComponent(out LayoutElement le)) le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        le.flexibleWidth = 0;
    }

    private static Image AddImage(RectTransform rt, Color color, Sprite sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var rt = Rect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    /// <summary>Sprite blanc à coins arrondis, généré par code (9-slice).</summary>
    private static Sprite MakeRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "RoundedRect"
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        float border = Mathf.Min(radius, size / 2f - 1f);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }
}
