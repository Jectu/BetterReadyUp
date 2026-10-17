using BepInEx;
using BepInEx.Configuration;
using System;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using RoR2.UI;
using UnityEngine.UI;
using TMPro;


// Please feel free to suggest fixes to my code.
namespace BetterReadyUp
{
    [BepInPlugin("com.jectu.betterreadyup", "Better Ready Up", "1.1.3")]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BetterReadyUpPlugin : BaseUnityPlugin
    {
        // settings, translations and all the ui bits i need to put back later
        private Texture2D optionsIconTexture;
        private Sprite optionsIconSprite;
        private ConfigEntry<int> multiplayerCountdown;
        private ConfigEntry<int> singleplayerCountdown;
        private VoteController vote;
        private readonly Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private Button forceReadyButton;
        private RectTransform forceReadyRect;
        private RectTransform forceReadyCanvasRect;
        private CharacterSelectController forceReadyController;
        private TextMeshProUGUI forceReadyLabel;


        private CharacterSelectController styledController;
        private bool? lastReadyState;
        private ColorBlock originalColors;
        private Sprite originalSprite;
        private Color originalImageColor;
        private TextMeshProUGUI readyLabel;
        private string originalLabel;
        private Image readyImage;


        private TextMeshProUGUI soloTimerLabel;
        private GameObject soloTimerPanel;
        private CharacterSelectController soloTimerController;
        private Image soloVoteIcon;
        private Sprite soloUnreadySprite;
        private Sprite soloReadySprite;
        private int lastSoloTimerSecond = -1;
        private bool soloTimerHasStarted;

        // sets everything up, solo stays vanilla unless someone enables its countdown
        private void Awake()
        {
            multiplayerCountdown = Config.Bind("Multiplayer", "Ready Countdown Seconds", 5,
                new ConfigDescription("Multiplayer: seconds after everyone readies before launch (host setting).",
                    new AcceptableValueRange<int>(0, 60)));

            singleplayerCountdown = Config.Bind("Singleplayer", "Ready Countdown Seconds", 0,
                new ConfigDescription("Single-player: seconds after pressing Ready before launch. Press Unready to cancel. Set to 0 to restore vanilla instant-start behaviour.",
                    new AcceptableValueRange<int>(0, 60)));

            LoadTranslations();
            TryRegisterOptionalRiskOfOptions();

            On.RoR2.PreGameController.OnEnable += PreGameEnabled;
            On.RoR2.PreGameController.OnDisable += PreGameDisabled;
            On.RoR2.UI.CharacterSelectController.ClientSetReady += ClientSetReadyToggle;
            On.RoR2.UI.CharacterSelectController.Update += CharacterSelectUpdate;
        }

        private void OnDestroy()
        {
            DestroyForceReadyButton();
            DestroySoloTimer();
            On.RoR2.PreGameController.OnEnable -= PreGameEnabled;
            On.RoR2.PreGameController.OnDisable -= PreGameDisabled;
            On.RoR2.UI.CharacterSelectController.ClientSetReady -= ClientSetReadyToggle;
            On.RoR2.UI.CharacterSelectController.Update -= CharacterSelectUpdate;
        }

        // load the txt beside the dll so people can fix the dodgy translations themselves
        private void LoadTranslations()
        {
            string dllDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
            string filePath = Path.Combine(dllDirectory, "translations.txt");
            if (!File.Exists(filePath)) filePath = Path.Combine(Path.GetDirectoryName(dllDirectory), "translations.txt");
            if (!File.Exists(filePath)) { Logger.LogWarning("[BETTERREADYUP] translations.txt not found. Using English."); return; }

            try
            {
                string language = null;
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        language = line.Substring(1, line.Length - 2).Trim();
                        if (!translations.ContainsKey(language))
                            translations[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        continue;
                    }
                    int equals = line.IndexOf('=');
                    if (language == null || equals < 1) continue;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (value.Length > 0) translations[language][key] = value;
                }
                Logger.LogInfo("[BETTERREADYUP] Loaded translations (" + translations.Count + " language sections) from " + filePath);
            }
            catch (Exception ex) { Logger.LogWarning("[BETTERREADYUP] Failed to load translations: " + ex.Message); }
        }

        // use the games language and fall back to english if a translation is missing
        private string GetTranslatedText(string key)
        {
            string language = RoR2.Language.currentLanguageName;
            Dictionary<string, string> values;
            string value;
            if (!string.IsNullOrEmpty(language) && translations.TryGetValue(language, out values) && values.TryGetValue(key, out value)) return value;
            if (translations.TryGetValue("en", out values) && values.TryGetValue(key, out value)) return value;
            switch (key)
            {
                case "Unready": return "Unready";
                case "ForceReady": return "Force Ready (ALL)";
                default: return key;
            }
        }

        // risk of options is optional apparently reflection keeps it that way so thats what I went with
        private void TryRegisterOptionalRiskOfOptions()
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
                    return;

                TryRegisterOptionalCountdownSliders();
                string dllDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
                string iconPath = Path.Combine(dllDirectory, "icon.png");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(Path.GetDirectoryName(dllDirectory), "icon.png");
                }
                if (!File.Exists(iconPath))
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options icon.png not found.");
                    return;
                }

                byte[] pngBytes = File.ReadAllBytes(iconPath);
                optionsIconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(optionsIconTexture, pngBytes, false))
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options icon.png is invalid.");
                    UnityEngine.Object.Destroy(optionsIconTexture);
                    optionsIconTexture = null;
                    return;
                }
                optionsIconTexture.name = "BetterReadyUpOptionsIconTexture";
                optionsIconSprite = Sprite.Create(optionsIconTexture,
                    new Rect(0f, 0f, optionsIconTexture.width, optionsIconTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                optionsIconSprite.name = "BetterReadyUpOptionsIcon";

                Type settingsManager = Type.GetType("RiskOfOptions.ModSettingsManager, RiskOfOptions", false);
                if (settingsManager == null)
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options settings API not found.");
                    return;
                }
                MethodInfo setIcon = settingsManager.GetMethod("SetModIcon",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Sprite), typeof(string), typeof(string) }, null);
                if (setIcon == null)
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options icon API not found.");
                    return;
                }
                setIcon.Invoke(null, new object[] { optionsIconSprite, "com.jectu.betterreadyup", "Better Ready Up" });
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BETTERREADYUP] Risk of Options icon setup failed: " + ex);
            }
        }

        // use the same config entries for the in game sliders so theres no extra syncing bullshit
        private void TryRegisterOptionalCountdownSliders()
        {
            try
            {
                Type managerType = Type.GetType("RiskOfOptions.ModSettingsManager, RiskOfOptions", false);
                Type optionType = Type.GetType("RiskOfOptions.Options.IntSliderOption, RiskOfOptions", false);
                Type sliderConfigType = Type.GetType("RiskOfOptions.OptionConfigs.IntSliderConfig, RiskOfOptions", false);
                Type baseOptionType = Type.GetType("RiskOfOptions.Options.BaseOption, RiskOfOptions", false);
                if (managerType == null || optionType == null || sliderConfigType == null || baseOptionType == null)
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options slider API not found.");
                    return;
                }

                MethodInfo addOption = managerType.GetMethod("AddOption",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { baseOptionType, typeof(string), typeof(string) }, null);
                if (addOption == null)
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options AddOption API not found.");
                    return;
                }

                ConstructorInfo sliderCtor = optionType.GetConstructor(new[] { typeof(ConfigEntry<int>), sliderConfigType });
                if (sliderCtor == null)
                {
                    Logger.LogWarning("[BETTERREADYUP] Risk of Options slider constructor not found.");
                    return;
                }

                object multiConfig = Activator.CreateInstance(sliderConfigType);
                object soloConfig = Activator.CreateInstance(sliderConfigType);
                SetSliderBounds(sliderConfigType, multiConfig);
                SetSliderBounds(sliderConfigType, soloConfig);

                object multiSlider = sliderCtor.Invoke(new object[] { multiplayerCountdown, multiConfig });
                object soloSlider = sliderCtor.Invoke(new object[] { singleplayerCountdown, soloConfig });
                object[] registration = { null, "com.jectu.betterreadyup", "Better Ready Up" };
                registration[0] = multiSlider;
                addOption.Invoke(null, registration);
                registration[0] = soloSlider;
                addOption.Invoke(null, registration);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BETTERREADYUP] Risk of Options slider setup failed: " + ex);
            }
        }

        private static void SetSliderBounds(Type configType, object config)
        {
            foreach (string name in new[] { "min", "max" })
            {
                int value = name == "min" ? 0 : 60;
                FieldInfo field = configType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null) field.SetValue(config, value);
                else
                {
                    PropertyInfo property = configType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (property == null || !property.CanWrite)
                        throw new MissingMemberException(configType.FullName, name);
                    property.SetValue(config, value, null);
                }
            }
        }

        // let vanilla handle solo at 0, otherwise set up the vote timer
        private void PreGameEnabled(On.RoR2.PreGameController.orig_OnEnable orig, PreGameController self)
        {
            orig(self);
            if (RoR2Application.isInSinglePlayer && singleplayerCountdown.Value == 0) return; // hands off solo unless they opt in
            vote = self.GetComponent<VoteController>();
            if (!vote) return;

            if (NetworkServer.active)
            {
                vote.timerStartCondition = VoteController.TimerStartCondition.WhileAllVotesReceived;
                int seconds = RoR2Application.isInSinglePlayer
                    ? singleplayerCountdown.Value : multiplayerCountdown.Value;
                vote.timeoutDuration = Mathf.Clamp(seconds, 0, 60);
                vote.canChangeVote = true;
                vote.canRevokeVote = true; // dont break the entire fucking point of the mod
                vote.mustTimeOut = true;
                vote.minimumTimeBeforeProcessing = 0f;
            }
        }

        private void PreGameDisabled(On.RoR2.PreGameController.orig_OnDisable orig, PreGameController self)
        {
            if (vote && vote.gameObject == self.gameObject) vote = null;
            RestoreReadyAppearance();
            DestroyForceReadyButton();
            DestroySoloTimer();
            orig(self);
        }

        // click ready again to unready, unless vanilla solo is handling it
        private void ClientSetReadyToggle(
            On.RoR2.UI.CharacterSelectController.orig_ClientSetReady orig,
            CharacterSelectController self)
        {
            if (!PreGameController.instance ||
                (RoR2Application.isInSinglePlayer && singleplayerCountdown.Value == 0))
            {
                orig(self);
                return;
            }

            if (self.IsClientReady())
                self.ClientSetUnready();
            else
                orig(self);
        }

        // vanilla likes resetting the ui so this undoes that after it updates
        private void CharacterSelectUpdate(
            On.RoR2.UI.CharacterSelectController.orig_Update orig,
            CharacterSelectController self)
        {
            orig(self);

            // Makes sure solo is just normal if the timer is turned off
            if (RoR2Application.isInSinglePlayer && singleplayerCountdown.Value == 0)
            {
                if (styledController) RestoreReadyAppearance();
                if (soloTimerPanel) DestroySoloTimer();
                return;
            }

            if (!PreGameController.instance ||
                !vote || !vote.enabled || !self.readyButton)
                return;

            if (!self.readyButton.gameObject.activeSelf)
                self.readyButton.gameObject.SetActive(true);

            if (self.unreadyButton && self.unreadyButton.gameObject != self.readyButton.gameObject &&
                self.unreadyButton.gameObject.activeSelf)
                self.unreadyButton.gameObject.SetActive(false);



            if (!self.readyButton.interactable)
                self.readyButton.interactable = true;

            bool isReady = self.IsClientReady();
            UpdateReadyAppearance(self, isReady);
            if (RoR2Application.isInSinglePlayer)
            {
                DestroyForceReadyButton();
                UpdateSoloTimer(self, isReady);
            }
            else
            {
                DestroySoloTimer();
                UpdateForceReadyButton(self);
            }
        }

        // changes the buttons back when leaving character select
        private void RestoreReadyAppearance()
        {
            if (styledController && styledController.readyButton)
            {
                var button = styledController.readyButton;
                button.colors = originalColors;
                if (readyImage)
                {
                    readyImage.sprite = originalSprite;
                    readyImage.color = originalImageColor;
                }
                if (readyLabel)
                    readyLabel.SetText(originalLabel);
            }

            styledController = null;
            lastReadyState = null;
            readyLabel = null;
            readyImage = null;
        }

        // swap ready to unready without rebuilding the damn button
        private void UpdateReadyAppearance(CharacterSelectController self, bool isReady)
        {
            if (styledController != self)
            {
                RestoreReadyAppearance();
                styledController = self;
                originalColors = self.readyButton.colors;
                readyImage = self.readyButton.targetGraphic as Image;
                if (!readyImage) readyImage = self.readyButton.GetComponent<Image>();
                originalSprite = readyImage ? readyImage.sprite : null;
                originalImageColor = readyImage ? readyImage.color : Color.white;
                var texts = self.readyButton.GetComponentsInChildren<TextMeshProUGUI>(true);
                readyLabel = null;
                foreach (var candidate in texts)
                {
                    if (candidate && candidate.text != null &&
                        candidate.text.Trim().Equals("READY", System.StringComparison.OrdinalIgnoreCase))
                    {
                        readyLabel = candidate;
                        break;
                    }
                }
                if (!readyLabel && texts.Length > 0) readyLabel = texts[0];
                originalLabel = readyLabel ? readyLabel.text : null;
            }

            bool soloInstant = RoR2Application.isInSinglePlayer && singleplayerCountdown.Value == 0;

            var desiredLabel = isReady ? GetTranslatedText("Unready") : originalLabel;
            if (readyLabel && !(soloInstant && isReady) && readyLabel.text != desiredLabel)
                readyLabel.SetText(desiredLabel);

            if (lastReadyState.HasValue && lastReadyState.Value == isReady)
                return;

            lastReadyState = isReady;
            var button = self.readyButton;

            if (isReady && !soloInstant)
            {
                if (self.unreadyButton) button.colors = self.unreadyButton.colors;

                Image unreadyImage = null;
                if (self.unreadyButton)
                {
                    unreadyImage = self.unreadyButton.targetGraphic as Image;
                    if (!unreadyImage) unreadyImage = self.unreadyButton.GetComponent<Image>();
                }
                if (readyImage)
                {
                    if (unreadyImage && unreadyImage.sprite)
                        readyImage.sprite = unreadyImage.sprite;

                    readyImage.color = new Color(0.85f, 0.22f, 0.22f, originalImageColor.a);
                }

            }
            else
            {
                button.colors = originalColors;
                if (readyImage)
                {
                    readyImage.sprite = originalSprite;
                    readyImage.color = originalImageColor;
                }
                if (readyLabel && !(soloInstant && isReady)) readyLabel.SetText(desiredLabel);
            }
        }

        private void DestroySoloTimer()
        {
            if (soloTimerPanel) Destroy(soloTimerPanel);
            soloTimerPanel = null;
            soloTimerLabel = null;
            soloTimerController = null;
            soloVoteIcon = null;
            soloUnreadySprite = null;
            soloReadySprite = null;
            lastSoloTimerSecond = -1;
            soloTimerHasStarted = false;
        }

        //cant be fucked making my own timer so just stealing the coop one and adding that and its indicator shit to solo for consistency when the timer is enabled.
        private void UpdateSoloTimer(CharacterSelectController controller, bool isReady)
        {
            if (!vote || !vote.enabled || !controller.readyButton || singleplayerCountdown.Value == 0)
            {
                DestroySoloTimer();
                return;
            }

            if (soloTimerController != controller || !soloTimerPanel)
            {
                DestroySoloTimer();
                Transform source = controller.transform.Find(
                    "SafeArea/ReadyPanel/VerticalLayoutGroup/VoteInfoPanel");
                if (!source)
                {
                    Logger.LogWarning("[BETTERREADYUP] Single-player timer panel not found.");
                    return;
                }

                soloTimerPanel = Instantiate(source.gameObject, source.parent, false);
                soloTimerPanel.name = "BetterReadyUp Native Solo VoteInfoPanel";
                soloTimerPanel.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);

                var nativeController = soloTimerPanel.GetComponent<VoteInfoPanelController>();
                if (nativeController) nativeController.enabled = false;

                var timerTransform = soloTimerPanel.transform.Find("TimerPanel/TimerLabel");
                soloTimerLabel = timerTransform
                    ? timerTransform.GetComponent<TextMeshProUGUI>() : null;
                var indicatorContainer = soloTimerPanel.transform.Find("IndicatorContainer");
                var indicator = soloTimerPanel.transform.Find("Indicator");
                if (indicatorContainer) indicatorContainer.gameObject.SetActive(true);
                if (indicator) indicator.gameObject.SetActive(true);
                if (indicator)
                {
                    foreach (var image in indicator.GetComponentsInChildren<Image>(true))
                    {
                        if (image.sprite)
                        {
                            soloVoteIcon = image;
                            image.gameObject.SetActive(true);
                            break;
                        }
                    }
                }
                if (soloVoteIcon)
                {
                    soloUnreadySprite = soloVoteIcon.sprite;
                    foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                    {
                        if (sprite.name == "texPlayerVoteStatus_1")
                        {
                            soloReadySprite = sprite;
                            break;
                        }
                    }
                    if (!soloReadySprite)
                        Logger.LogWarning("[BETTERREADYUP] Single-player ready icon not found.");
                }
                if (!soloVoteIcon)
                    Logger.LogWarning("[BETTERREADYUP] Single-player status icon not found.");
                if (!soloTimerLabel)
                {
                    Logger.LogWarning("[BETTERREADYUP] Single-player timer label not found.");
                    DestroySoloTimer();
                    return;
                }

                foreach (var graphic in soloTimerPanel.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                foreach (var raycaster in soloTimerPanel.GetComponentsInChildren<GraphicRaycaster>(true))
                    raycaster.enabled = false;
                foreach (var selectable in soloTimerPanel.GetComponentsInChildren<Selectable>(true))
                    selectable.enabled = false;
                soloTimerPanel.SetActive(true);
                soloTimerController = controller;
            }

            if (!soloTimerPanel.activeSelf) soloTimerPanel.SetActive(true);
            if (!isReady) soloTimerHasStarted = false;
            if (isReady && vote.timerIsActive) soloTimerHasStarted = true;
            int remaining;
            if (!isReady)
                remaining = singleplayerCountdown.Value;
            else if (!vote.timerIsActive && soloTimerHasStarted)
                remaining = 0;
            else if (vote.timerIsActive)
                remaining = Mathf.Max(0, Mathf.CeilToInt(vote.timer - 0.05f));
            else
                remaining = singleplayerCountdown.Value;

            if (remaining != lastSoloTimerSecond)
            {
                lastSoloTimerSecond = remaining;
                soloTimerLabel.SetText("0:" + remaining.ToString("00"));
            }
            if (soloVoteIcon)
            {
                soloVoteIcon.sprite = isReady && soloReadySprite
                    ? soloReadySprite : soloUnreadySprite;
                soloVoteIcon.color = Color.white;
            }

        }


        // if the host presses the force everyone button it readies up for anyone not ready
        private void ForceEveryoneReady()
        {
            if (!NetworkServer.active || !vote || !vote.enabled) return;

            for (int i = 0; i < vote.GetVoteCount(); i++)
            {
                UserVote entry = vote.GetVote(i);
                if (!entry.networkUserObject || entry.receivedVote) continue;

                NetworkUser user = entry.networkUserObject.GetComponent<NetworkUser>();
                if (user) vote.ReceiveUserVote(user, 0);
            }
        }

        private void DestroyForceReadyButton()
        {
            if (forceReadyButton) Destroy(forceReadyButton.gameObject);
            forceReadyButton = null;
            forceReadyRect = null;
            forceReadyCanvasRect = null;
            forceReadyController = null;
            forceReadyLabel = null;
        }

        private static TextMeshProUGUI FindButtonLabel(Button button, string label)
        {
            foreach (var candidate in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (candidate && string.Equals(candidate.text?.Trim(), label,
                    System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return null;
        }

        // stealing the native back button for force ready because my own styling looked like shit, hopefully an update doesnt break this bodge. 
        private void UpdateForceReadyButton(CharacterSelectController controller)
        {
            if (!NetworkServer.active || Application.isBatchMode || !vote || !vote.enabled ||
                RoR2Application.isInSinglePlayer)
            {
                DestroyForceReadyButton();
                return;
            }

            if (forceReadyController != controller || !forceReadyButton)
            {
                DestroyForceReadyButton();
                var canvas = controller.readyButton.GetComponentInParent<Canvas>();
                if (!canvas) return;
                canvas = canvas.rootCanvas;
                forceReadyCanvasRect = canvas.transform as RectTransform;
                if (!forceReadyCanvasRect) return;

                Button source = null;
                foreach (var candidate in Resources.FindObjectsOfTypeAll<Button>()) // in the past it tried to find the back button with just the word back. Turns out other languages exist
                {
                    if (!candidate || !candidate.gameObject.activeInHierarchy ||
                        candidate == controller.readyButton || !candidate.GetComponentInParent<Canvas>())
                        continue;
                    bool isBackButton = candidate.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        FindButtonLabel(candidate, "Back");
                    if (!isBackButton)
                    {
                        foreach (var loc in candidate.GetComponentsInChildren<LanguageTextMeshController>(true))
                        {
                            string token = loc ? loc.token : null;
                            if (!string.IsNullOrEmpty(token) &&
                                (token.Equals("BACK", StringComparison.OrdinalIgnoreCase) ||
                                 token.EndsWith("_BACK", StringComparison.OrdinalIgnoreCase) ||
                                 token.StartsWith("BACK_", StringComparison.OrdinalIgnoreCase)))
                            {
                                isBackButton = true;
                                break;
                            }
                        }
                    }
                    if (isBackButton)
                    {
                        source = candidate;
                        break;
                    }
                }
                if (!source) return;

                GameObject copy = Instantiate(source.gameObject, forceReadyCanvasRect, false);
                copy.name = "BetterReadyUp Force Ready (ALL)";
                forceReadyButton = copy.GetComponent<Button>();
                forceReadyRect = copy.transform as RectTransform;
                forceReadyController = controller;
                if (!forceReadyButton || !forceReadyRect)
                {
                    Destroy(copy);
                    DestroyForceReadyButton();
                    return;
                }

                forceReadyButton.onClick = new Button.ButtonClickedEvent(); // dont accidentally make force ready leave the lobby
                forceReadyButton.onClick.AddListener(ForceEveryoneReady);
                forceReadyButton.interactable = true;
                forceReadyButton.navigation = new Navigation { mode = Navigation.Mode.None };
                forceReadyLabel = forceReadyButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (!forceReadyLabel)
                {
                    var labels = copy.GetComponentsInChildren<TextMeshProUGUI>(true);
                    if (labels.Length > 0) forceReadyLabel = labels[0];
                }
                foreach (var loc in copy.GetComponentsInChildren<LanguageTextMeshController>(true)) // stop the clone changing itself back to saying back
                    Destroy(loc);
                if (forceReadyLabel) forceReadyLabel.SetText(GetTranslatedText("ForceReady"));

                forceReadyRect.anchorMin = forceReadyRect.anchorMax = new Vector2(0.5f, 0.5f);
                forceReadyRect.pivot = new Vector2(0.5f, 0.5f);
                forceReadyRect.localScale = Vector3.one;
                forceReadyRect.localRotation = Quaternion.identity;
                copy.SetActive(true);
                forceReadyRect.SetAsLastSibling();
            }

            if (!forceReadyRect || !forceReadyCanvasRect || !controller.readyButton) return;

            string forceReadyText = GetTranslatedText("ForceReady");
            if (forceReadyLabel && forceReadyLabel.text != forceReadyText)
                forceReadyLabel.SetText(forceReadyText);

            RectTransform readyRect = controller.readyButton.transform as RectTransform;
            if (!readyRect) return;
            Vector3[] corners = new Vector3[4];
            readyRect.GetWorldCorners(corners); // anchor under ready so ui scaling doesnt screw up placement
            Vector3 bottomMiddle = (corners[0] + corners[3]) * 0.5f;
            Vector3 heightVector = corners[1] - corners[0];
            Vector3 widthVector = corners[3] - corners[0];

            Vector3 centre = bottomMiddle - heightVector * 1.21f;
            Vector3 canvasCentre = forceReadyCanvasRect.InverseTransformPoint(centre);
            Vector3 canvasWidth = forceReadyCanvasRect.InverseTransformVector(widthVector);
            Vector3 canvasHeight = forceReadyCanvasRect.InverseTransformVector(heightVector);
            forceReadyRect.anchoredPosition = new Vector2(canvasCentre.x, canvasCentre.y);
            forceReadyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Abs(canvasWidth.x));
            forceReadyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Abs(canvasHeight.y) * 0.72f);
        }
    }
}
