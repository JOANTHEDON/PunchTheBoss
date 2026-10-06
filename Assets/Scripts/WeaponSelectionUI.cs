using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns and manages the 5 weapon action buttons (Glove, Slap, Coffee, Chappal, Super)
/// in the bottom UI panel after the image is applied/uploaded.
/// </summary>
public class WeaponSelectionUI : MonoBehaviour {

    [Header("Weapon Bar Container")]
    [Tooltip("Panel holding the 5 weapon buttons")]
    [SerializeField] private GameObject weaponPanel;

    [Header("Buttons")]
    [SerializeField] private Button gloveBtn;
    [SerializeField] private Button slapBtn;
    [SerializeField] private Button coffeeBtn;
    [SerializeField] private Button chappalBtn;
    [SerializeField] private Button superBtn;

    [Header("Upload Button Reference")]
    [SerializeField] private GameObject uploadBtn;

    private void Start() {
        SetupButtons();
    }

    private void SetupButtons() {
        if (gloveBtn != null) gloveBtn.onClick.AddListener(() => Select(WeaponType.Glove));
        if (slapBtn != null) slapBtn.onClick.AddListener(() => Select(WeaponType.Slap));
        if (coffeeBtn != null) coffeeBtn.onClick.AddListener(() => Select(WeaponType.Coffee));
        if (chappalBtn != null) chappalBtn.onClick.AddListener(() => Select(WeaponType.Chappal));
        if (superBtn != null) superBtn.onClick.AddListener(() => Select(WeaponType.Super));
    }

    private void Select(WeaponType weapon) {
        if (WeaponManager.Instance != null) {
            WeaponManager.Instance.SelectWeapon(weapon);
        }
    }

    /// <summary>
    /// Shows the weapon panel and activates the 5 attack buttons.
    /// Called when the face image is applied.
    /// </summary>
    public void ShowWeaponPanel(bool show = true) {
        if (weaponPanel != null) {
            weaponPanel.SetActive(show);
        }

        // Default to glove upon opening
        if (show && WeaponManager.Instance != null) {
            WeaponManager.Instance.SelectWeapon(WeaponType.Glove);
        }
    }

    /// <summary>
    /// Programmatically create the 5 buttons UI if not wired in the scene Inspector.
    /// </summary>
    public void BuildRuntimeUI(Transform parentCanvas) {
        if (weaponPanel != null) return;

        // Container
        GameObject panelObj = new GameObject("WeaponActionPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panelObj.transform.SetParent(parentCanvas, false);
        weaponPanel = panelObj;

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(900f, 130f);

        HorizontalLayoutGroup hlg = panelObj.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Create the 5 buttons
        gloveBtn = CreateWeaponButton(panelObj.transform, "🥊\nGlove", () => Select(WeaponType.Glove), new Color(1f, 0.4f, 0.4f));
        slapBtn = CreateWeaponButton(panelObj.transform, "👋\nSlap", () => Select(WeaponType.Slap), new Color(1f, 0.85f, 0.4f));
        coffeeBtn = CreateWeaponButton(panelObj.transform, "☕\nCoffee", () => Select(WeaponType.Coffee), new Color(0.8f, 0.6f, 0.4f));
        chappalBtn = CreateWeaponButton(panelObj.transform, "🩴\nChappal", () => Select(WeaponType.Chappal), new Color(0.6f, 0.85f, 1f));
        superBtn = CreateWeaponButton(panelObj.transform, "💥\nSUPER", () => Select(WeaponType.Super), new Color(1f, 0.3f, 0.8f));

        weaponPanel.SetActive(false); // Initially hidden until image applied
    }

    private Button CreateWeaponButton(Transform parent, string title, UnityEngine.Events.UnityAction action, Color bgColor) {
        GameObject btnObj = new GameObject("Btn_" + title.Replace("\n", "_"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150f, 110f);

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(action);

        // Text
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        Text txt = textObj.GetComponent<Text>();
        txt.text = title;
        txt.fontSize = 26;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;

        return btn;
    }
}
