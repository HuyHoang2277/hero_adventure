#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cửa sổ Unity Editor (UI Toolkit) chạy mô phỏng Monte-Carlo 100-2000 trận/màn để kiểm định cân bằng
/// theo cấp nhân vật, Talent, Trang bị và xuất JSON cấu hình cho Unity Remote Config.
/// Mở tại: Tools > Hero Adventure > Balance Simulator
/// </summary>
public class BalanceSimulatorWindow : EditorWindow
{
    private SliderInt playerLevelSlider;
    private SliderInt atkTalentSlider;
    private SliderInt defTalentSlider;
    private SliderInt gemTalentSlider;
    private EnumField gearPresetField;
    private SliderInt runsSlider;
    private ObjectField balanceConfigField;
    private VisualElement resultsContainer;
    private Label statusLabel;

    [MenuItem("Tools/Hero Adventure/Balance Simulator")]
    public static void OpenWindow()
    {
        BalanceSimulatorWindow wnd = GetWindow<BalanceSimulatorWindow>();
        wnd.titleContent = new GUIContent("Balance Simulator");
        wnd.minSize = new Vector2(640f, 560f);
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingLeft = 14;
        root.style.paddingRight = 14;
        root.style.paddingTop = 12;
        root.style.paddingBottom = 12;

        Label header = new Label("Hero's Adventure — Balance Simulator (Monte-Carlo)");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 8;
        root.Add(header);

        // Khối cấu hình đầu vào
        VisualElement configBox = new VisualElement();
        configBox.style.backgroundColor = new Color(0.16f, 0.17f, 0.20f, 1f);
        configBox.style.paddingLeft = 10;
        configBox.style.paddingRight = 10;
        configBox.style.paddingTop = 8;
        configBox.style.paddingBottom = 8;
        configBox.style.marginBottom = 10;
        configBox.style.borderTopLeftRadius = 6;
        configBox.style.borderTopRightRadius = 6;
        configBox.style.borderBottomLeftRadius = 6;
        configBox.style.borderBottomRightRadius = 6;

        balanceConfigField = new ObjectField("BalanceConfigSO")
        {
            objectType = typeof(BalanceConfigSO),
            value = Resources.Load<BalanceConfigSO>("SO/BalanceConfig")
        };
        configBox.Add(balanceConfigField);

        playerLevelSlider = new SliderInt("Hero Level", 1, 20) { value = 1, showInputField = true };
        atkTalentSlider = new SliderInt("Attack Talent Lv", 0, 10) { value = 0, showInputField = true };
        defTalentSlider = new SliderInt("Defense Talent Lv", 0, 10) { value = 0, showInputField = true };
        gemTalentSlider = new SliderInt("Gem Mastery Lv", 0, 10) { value = 0, showInputField = true };
        gearPresetField = new EnumField("Equipment Preset", SimGearPreset.None);
        runsSlider = new SliderInt("Sim Runs / Stage", 100, 2000) { value = 500, showInputField = true };

        configBox.Add(playerLevelSlider);
        configBox.Add(atkTalentSlider);
        configBox.Add(defTalentSlider);
        configBox.Add(gemTalentSlider);
        configBox.Add(gearPresetField);
        configBox.Add(runsSlider);
        root.Add(configBox);

        // Thanh nút thao tác
        VisualElement btnRow = new VisualElement();
        btnRow.style.flexDirection = FlexDirection.Row;
        btnRow.style.marginBottom = 10;

        Button runLevelsBtn = new Button(RunAllLevelsSimulation) { text = "Simulate Levels 1-5" };
        runLevelsBtn.style.flexGrow = 1;
        runLevelsBtn.style.height = 30;
        runLevelsBtn.style.marginRight = 6;

        Button runEndlessBtn = new Button(RunEndlessSimulation) { text = "Simulate Endless" };
        runEndlessBtn.style.flexGrow = 1;
        runEndlessBtn.style.height = 30;
        runEndlessBtn.style.marginRight = 6;

        Button createConfigBtn = new Button(CreateOrSelectBalanceConfigAsset) { text = "Create BalanceConfigSO" };
        createConfigBtn.style.height = 30;
        createConfigBtn.style.marginRight = 6;

        Button exportJsonBtn = new Button(ExportRemoteConfigJson) { text = "Copy Remote JSON" };
        exportJsonBtn.style.height = 30;

        btnRow.Add(runLevelsBtn);
        btnRow.Add(runEndlessBtn);
        btnRow.Add(createConfigBtn);
        btnRow.Add(exportJsonBtn);
        root.Add(btnRow);

        statusLabel = new Label("Ready. Pick Hero build above and click Simulate.");
        statusLabel.style.marginBottom = 8;
        statusLabel.style.color = new Color(0.75f, 0.84f, 0.96f, 1f);
        root.Add(statusLabel);

        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        resultsContainer = new VisualElement();
        scroll.Add(resultsContainer);
        root.Add(scroll);
    }

    private SimConfig BuildSimConfig()
    {
        BalanceConfigSO so = balanceConfigField.value as BalanceConfigSO;
        SimConfig cfg = SimConfig.CreateDefault(so);
        cfg.PlayerLevel = playerLevelSlider.value;
        cfg.AttackTalentLevel = atkTalentSlider.value;
        cfg.DefenseTalentLevel = defTalentSlider.value;
        cfg.GemTalentLevel = gemTalentSlider.value;
        cfg.GearPreset = (SimGearPreset)gearPresetField.value;
        cfg.Runs = runsSlider.value;
        return cfg;
    }

    private void RunAllLevelsSimulation()
    {
        resultsContainer.Clear();

        CharacterInfoSO playerSO = Resources.Load<CharacterInfoSO>("SO/CharacterInfo/Player1");
        LevelConfig[] levels = Resources.LoadAll<LevelConfig>("SO/LevelInfo");
        if (levels == null || levels.Length == 0)
        {
            statusLabel.text = "Không tìm thấy LevelConfig trong Resources/SO/LevelInfo!";
            return;
        }
        System.Array.Sort(levels, (a, b) => a.LevelID.CompareTo(b.LevelID));

        List<CharacterInfoSO> fallbackEnemies = LoadDefaultEnemyPool();
        SimConfig cfg = BuildSimConfig();

        double startTime = EditorApplication.timeSinceStartup;
        foreach (LevelConfig lv in levels)
        {
            LevelSimulationReport rep = CombatSimulator.SimulateLevel(playerSO, lv, fallbackEnemies, cfg);
            AddLevelReportRow(rep);
        }
        double elapsedMs = (EditorApplication.timeSinceStartup - startTime) * 1000.0;

        statusLabel.text = $"Done: Simulated {levels.Length} levels x {cfg.Runs} runs in {elapsedMs:0} ms (Lv.{cfg.PlayerLevel}, Gear: {cfg.GearPreset}).";
    }

    private void RunEndlessSimulation()
    {
        resultsContainer.Clear();

        CharacterInfoSO playerSO = Resources.Load<CharacterInfoSO>("SO/CharacterInfo/Player1");
        CharacterInfoSO bossSO = Resources.Load<CharacterInfoSO>("SO/CharacterInfo/Golem");
        List<CharacterInfoSO> enemyPool = LoadDefaultEnemyPool();
        SimConfig cfg = BuildSimConfig();

        double startTime = EditorApplication.timeSinceStartup;
        EndlessSimulationReport rep = CombatSimulator.SimulateEndless(playerSO, enemyPool, bossSO, cfg);
        double elapsedMs = (EditorApplication.timeSinceStartup - startTime) * 1000.0;

        VisualElement card = CreateResultCard();
        Label title = new Label($"ENDLESS MODE ({rep.Runs} runs)");
        title.style.fontSize = 15;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        card.Add(title);

        card.Add(new Label($"Avg Cleared Rounds: {rep.AvgClearedRounds:0.00}   |   Median: {rep.MedianClearedRounds}   |   Max: {rep.MaxClearedRounds}"));
        card.Add(new Label($"Round 5 Boss Clear Rate: {rep.Round5BossClearRatePercent:0.0}%"));
        Label diag = new Label($"Diagnosis: {rep.Diagnosis}");
        diag.style.color = new Color(0.98f, 0.84f, 0.36f, 1f);
        card.Add(diag);

        resultsContainer.Add(card);
        statusLabel.text = $"Done: Simulated Endless x {cfg.Runs} runs in {elapsedMs:0} ms.";
    }

    private void AddLevelReportRow(LevelSimulationReport rep)
    {
        VisualElement card = CreateResultCard();

        VisualElement topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.justifyContent = Justify.SpaceBetween;

        string bossTag = rep.IsBossLevel ? " [BOSS]" : "";
        Label title = new Label($"Level {rep.LevelID}{bossTag}");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 14;

        Label diag = new Label(rep.Diagnosis);
        diag.style.color = GetWinRateColor(rep.WinRatePercent);

        topRow.Add(title);
        topRow.Add(diag);
        card.Add(topRow);

        // Thanh tiến độ Win Rate trực quan
        VisualElement barTrack = new VisualElement();
        barTrack.style.height = 14;
        barTrack.style.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        barTrack.style.marginTop = 4;
        barTrack.style.marginBottom = 4;

        VisualElement barFill = new VisualElement();
        barFill.style.height = Length.Percent(100);
        barFill.style.width = Length.Percent(Mathf.Clamp(rep.WinRatePercent, 0f, 100f));
        barFill.style.backgroundColor = GetWinRateColor(rep.WinRatePercent);
        barTrack.Add(barFill);
        card.Add(barTrack);

        Label metrics = new Label(
            $"Win Rate: {rep.WinRatePercent:0.0}%   |   3-Star: {rep.ThreeStarRatePercent:0.0}%   |   Avg Turns: {rep.AvgTurns:0.0}   |   Win Avg HP: {rep.AvgRemainingHpPercent:0.0}%");
        metrics.style.fontSize = 12;
        card.Add(metrics);

        resultsContainer.Add(card);
    }

    private VisualElement CreateResultCard()
    {
        VisualElement card = new VisualElement();
        card.style.backgroundColor = new Color(0.18f, 0.19f, 0.23f, 1f);
        card.style.paddingLeft = 10;
        card.style.paddingRight = 10;
        card.style.paddingTop = 8;
        card.style.paddingBottom = 8;
        card.style.marginBottom = 8;
        card.style.borderTopLeftRadius = 5;
        card.style.borderTopRightRadius = 5;
        card.style.borderBottomLeftRadius = 5;
        card.style.borderBottomRightRadius = 5;
        return card;
    }

    private static Color GetWinRateColor(float winRate)
    {
        if (winRate < 45f) return new Color(0.92f, 0.30f, 0.26f, 1f);
        if (winRate <= 92f) return new Color(0.24f, 0.76f, 0.44f, 1f);
        return new Color(0.95f, 0.72f, 0.20f, 1f);
    }

    private static List<CharacterInfoSO> LoadDefaultEnemyPool()
    {
        List<CharacterInfoSO> pool = new List<CharacterInfoSO>();
        string[] names = { "Slime", "Skeleton", "Orc" };
        foreach (string n in names)
        {
            CharacterInfoSO so = Resources.Load<CharacterInfoSO>($"SO/CharacterInfo/{n}");
            if (so != null) pool.Add(so);
        }
        return pool;
    }

    private void CreateOrSelectBalanceConfigAsset()
    {
        const string path = "Assets/Resources/SO/BalanceConfig.asset";
        BalanceConfigSO existing = AssetDatabase.LoadAssetAtPath<BalanceConfigSO>(path);
        if (existing == null)
        {
            existing = CreateInstance<BalanceConfigSO>();
            AssetDatabase.CreateAsset(existing, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        balanceConfigField.value = existing;
        Selection.activeObject = existing;
        statusLabel.text = $"BalanceConfigSO ready at {path}.";
    }

    private void ExportRemoteConfigJson()
    {
        BalanceConfigSO so = balanceConfigField.value as BalanceConfigSO;
        if (so == null) so = CreateInstance<BalanceConfigSO>();
        string json = so.ToRemoteConfigJson();
        EditorGUIUtility.systemCopyBuffer = json;
        statusLabel.text = "Copied BalanceConfigSO JSON to clipboard (ready for Unity Remote Config)!";
        Debug.Log($"[BalanceConfigSO Remote Config JSON]\n{json}");
    }
}
#endif
