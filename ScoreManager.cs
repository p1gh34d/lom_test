using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System;
using System.Linq;

public class ScoreManager : MonoBehaviour
{
    private NoteInputManager noteInputManager;
    private NoteSpawner noteSpawner;
    private ChartParser chartParser; // Ссылка на ChartParser

    // Новые поля для UI песни
    [SerializeField] private Image songCoverImage; // Кавер
    [SerializeField] private Text songTitleText;   // Название песни
    [SerializeField] private Text bandNameText;    // Название группы
    [SerializeField] private Sprite defaultCover;  // Дефолтный кавер

    [SerializeField] private Text multiplierText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text streakText;

    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Image statsSongCoverImage; // Кавер для statsPanel
    [SerializeField] private Text statsBandNameText;   // Название группы для statsPanel
    [SerializeField] private Text statsSongTitleText;  // Название песни для statsPanel
    [SerializeField] private Text statsSongCharterText;  // Название песни для statsPanel
    [SerializeField] private Text statsScoreText;
    [SerializeField] private Text statsProgressText;
    [SerializeField] private Text statsStarsText;
    [SerializeField] private Text statsTotalNotesText;
    [SerializeField] private Text statsMaxStreakText;
    [SerializeField] private Text statsHitNotesText;
    [SerializeField] private Text statsMissedNotesText;
    [SerializeField] private Text statsPerfectText;
    [SerializeField] private Text statsEarlyText;
    [SerializeField] private Text statsLateText;
    [SerializeField] private Text statsAccuracyText;

    [SerializeField] private Text statsPreviousScoreText;
    //[SerializeField] private Text statsPreviousProgressText;
    //[SerializeField] private Text statsPreviousStarsText;
    [SerializeField] private Text statsPreviousMaxStreakText;
    [SerializeField] private Text statsPreviousHitNotesText;
    //[SerializeField] private Text statsPreviousMissedNotesText;
    //[SerializeField] private Text statsPreviousAccuracyText;
    //[SerializeField] private Text statsPreviousPerfectText;
    //[SerializeField] private Text statsPreviousEarlyText;
    //[SerializeField] private Text statsPreviousLateText;

    // Новое поле для префаба точности
    [SerializeField] private Text accuracyText; // Уже есть, используем его
    [SerializeField] private Text offsetText;
    private Vector3 initialAccuracyPosition;
    private Vector3 initialOffsetPosition;
    private GameObject lastAccuracyInstance;
    private GameObject lastOffsetInstance;

    private int score = 0;
    private int streak = 0;
    private int maxStreak = 0;
    private int multiplier = 1;
    private int hitNotes = 0;
    private int missedNotes = 0;
    private int totalNotes = 0;
    private int perfectHits = 0;
    private int earlyHits = 0;
    private int lateHits = 0;
    private const int maxMultiplierLimit = 4;
    private const int pointsPerNote = 50;
    private float starPowerEnergy = 0f; // Уровень энергии Star Power (0–1)
    private const float starPowerDetectionWindow = 0.2f;

    [SerializeField] private GameObject starPowerModel;
    [SerializeField] private GameObject starPowerEffect;
    [SerializeField] private AudioClip starPowerEnergyAddClip; // Звук для добавления энергии Star Power
    [SerializeField] private AudioClip starPowerActivateClip;  // Звук для активации Star Power
    private Vector3 initialSPEnergyScale;
    private Vector3 initialSPEnergyPosition;

    private HashSet<int> processedChordsThisFrame = new HashSet<int>();
    private HashSet<int> missedChordsThisFrame = new HashSet<int>();
    private HashSet<GameObject> missedNotesSet = new HashSet<GameObject>();

private HashSet<int> processedStarPowerNotes;
private HashSet<int> processedStarPowerChordsThisFrame = new HashSet<int>();
private HashSet<int> sectionEnergyAwarded = new HashSet<int>();
private Dictionary<int, HashSet<int>> processedTicksPerSection = new Dictionary<int, HashSet<int>>();
private Dictionary<int, bool> starPowerSectionSuccess; // Отслеживание успешности секций


    private Coroutine whammyEnergyCoroutine = null;
    private Coroutine starPowerCoroutine = null;

    private bool canStartNewWhammyCycle = true;
    private bool isAddingEnergy = false;
    private bool isStarPowerActive = false;
    private bool useAccuracySystem;
    private bool hasStartedPlaying = false;

    public event Action<float> OnCalibrationHit;
    private float noteOffset;

    private void Start()
    {
        noteInputManager = FindObjectOfType<NoteInputManager>();
        noteSpawner = FindObjectOfType<NoteSpawner>();

        noteOffset = UserManager.Instance.GetCurrentUser()?.audioOffset ?? 0f;

        if (noteInputManager == null || noteSpawner == null)
        {
            Debug.LogError("NoteController or NoteSpawner not found in scene!");
            return;
        }

    // Создаём AudioSource скриптом
    AudioSource audioSource = gameObject.AddComponent<AudioSource>();
    audioSource.playOnAwake = false;
    audioSource.loop = false;
    audioSource.volume = 0.8f; // Настрой громкость (0–1)

        if (starPowerModel != null)
        {
            Transform speTransform = starPowerModel.transform.Find("SPEnergy");
            if (speTransform != null)
            {
                initialSPEnergyScale = speTransform.localScale;
                initialSPEnergyPosition = speTransform.localPosition;
                if (starPowerEnergy == 0f)
                {
                    speTransform.gameObject.SetActive(false);
                }
            }
        }

    if (starPowerEffect == null)
    {
        Debug.LogError("StarPower GameObject not found in scene!");
    }

        processedStarPowerNotes = new HashSet<int>();
        starPowerSectionSuccess = new Dictionary<int, bool>();

        noteInputManager.OnNoteHit += HandleNoteHit;
        noteInputManager.OnNoteSustainEnd += HandleNoteSustainEnd;
        noteInputManager.OnNoteSustainTick += HandleNoteSustainTick;

        OnCalibrationHit -= HandleCalibrationHit;
        OnCalibrationHit += HandleCalibrationHit;

        useAccuracySystem = PlayerPrefs.GetInt($"AccuracySystem_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (accuracyText != null)
        {
            accuracyText.text = "";
            initialAccuracyPosition = accuracyText.transform.position;
        }
        else
        {
            Debug.LogError("AccuracyText is null in Start!");
        }

        if (offsetText != null)
        {
            offsetText.text = "";
            initialOffsetPosition = offsetText.transform.position;
            offsetText.gameObject.SetActive(IsCalibrationActive() && IsFirstStage());
        }
        else
        {
            Debug.LogError("OffsetText is null in Start!");
        }

        statsPanel.SetActive(false);
        UpdateSongInfo();
        StartCoroutine(InitializeAfterSongLoad());
        UpdateTextVisibility();
    }

    private bool IsCalibrationActive()
    {
        return PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    }

    private bool IsFirstStage()
    {
        return PlayerPrefs.GetInt("CalibrationStage", 1) == 1;
    }

    public void TriggerCalibrationHit(float offsetMs)
    {
        OnCalibrationHit?.Invoke(offsetMs);
    }

    private void HandleCalibrationHit(float offsetMs)
    {
        if (!IsCalibrationActive() || !IsFirstStage())
        {
            return;
        }

        bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (showAccuracy)
        {
            if (offsetText == null)
            {
                Debug.LogError("offsetText is null, cannot show offset!");
                return;
            }
            StartCoroutine(ShowCalibrationOffset(offsetMs));
        }
        else
        {
            Debug.LogWarning("showAccuracy is false, offset text not shown");
        }
    }

    private void UpdateTextVisibility()
    {
        bool isCalibration = IsCalibrationActive();
        bool isFirstStage = IsFirstStage();
        if (accuracyText != null)
        {
            accuracyText.gameObject.SetActive(!isCalibration || !isFirstStage);
        }
        if (offsetText != null)
        {
            offsetText.gameObject.SetActive(isCalibration && isFirstStage);
        }
        else
        {
            Debug.LogError("offsetText is null in UpdateTextVisibility!");
        }
    }

private void UpdateSongInfo()
{
    bool isCalibration = IsCalibrationActive();

    // Название песни
    if (songTitleText != null)
    {
        songTitleText.text = PlayerPrefs.GetString("SongTitle", isCalibration ? "Calibration" : "Unknown Song");
    }

    // Название группы
    if (bandNameText != null)
    {
        bandNameText.text = PlayerPrefs.GetString("BandName", isCalibration ? "Messiah Flesh" : "Unknown Artist");
    }

    // Кавер
    if (songCoverImage != null)
    {
        string coverPath = PlayerPrefs.GetString("CoverPath", "default");
        Debug.Log($"Loading cover: CoverPath={coverPath}");

        if (coverPath != "default")
        {
            if (Application.isEditor)
            {
                // В редакторе используем полный путь
                if (File.Exists(coverPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(coverPath);
                        Texture2D texture = new Texture2D(2, 2);
                        if (texture.LoadImage(bytes))
                        {
                            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                            songCoverImage.sprite = sprite;
                            Debug.Log($"Loaded cover from file: {coverPath}");
                        }
                        else
                        {
                            Debug.LogError($"Failed to load image bytes: {coverPath}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error loading cover file: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"Cover file not found: {coverPath}");
                }
            }
            else if (isCalibration)
            {
                string resourcePath = coverPath; // Ожидаем Sounds/calibration/cover
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    songCoverImage.sprite = sprite;
                }
                else
                {
                    Debug.LogError($"Cover not found in Resources: {resourcePath}");
                }
            }
            else
            {
                if (coverPath != "default" && File.Exists(coverPath))
                {
                    byte[] bytes = File.ReadAllBytes(coverPath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    songCoverImage.sprite = sprite;
                }
                else if (defaultCover != null)
                {
                    songCoverImage.sprite = defaultCover;
                }
            }
        }

        if (songCoverImage.sprite == null && defaultCover != null)
        {
            songCoverImage.sprite = defaultCover;
            Debug.Log($"Using default cover.");
        }
    }

    // Обновляем UI для statsPanel
    if (statsBandNameText != null)
    {
        statsBandNameText.text = PlayerPrefs.GetString("BandName", isCalibration ? "Messiah Flesh" : "Unknown Artist");
    }

    if (statsSongTitleText != null)
    {
        statsSongTitleText.text = PlayerPrefs.GetString("SongTitle", isCalibration ? "Calibration" : "Unknown Song");
    }

    if (statsSongCharterText != null)
    {
        string charter = PlayerPrefs.GetString("SongCharter", "Unknown Charter");
        statsSongCharterText.text = string.IsNullOrEmpty(charter) || charter == "Unknown Charter" ? "Unknown Charter" : $"By {charter}";
    }
}

    private System.Collections.IEnumerator InitializeAfterSongLoad()
    {
        yield return new WaitUntil(() => noteSpawner.CurrentSongData != null);
        CalculateTotalNotes();
        UpdateUI();
        yield return new WaitUntil(() => noteSpawner.AudioSource.isPlaying);
        StartCoroutine(CheckSongEnd());
    }

private void HandleNoteHit(int midi, bool isLongNote, NoteInputManager.HitAccuracy accuracy)
{
    if (IsCalibrationActive() && IsFirstStage()) return;

    int chordId = Time.frameCount;
    if (!processedChordsThisFrame.Contains(chordId))
    {
        hitNotes++;
        streak++;
        if (streak > maxStreak) maxStreak = streak;
        processedChordsThisFrame.Add(chordId);
        UpdateMultiplier();

        if (useAccuracySystem && accuracy != NoteInputManager.HitAccuracy.None)
        {
            switch (accuracy)
            {
                case NoteInputManager.HitAccuracy.Perfect:
                    perfectHits++;
                    break;
                case NoteInputManager.HitAccuracy.Early:
                    earlyHits++;
                    break;
                case NoteInputManager.HitAccuracy.Late:
                    lateHits++;
                    break;
            }

            bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
            if (showAccuracy)
            {
                StartCoroutine(ShowAccuracy(accuracy));
            }
        }
    }

    AddScore(pointsPerNote);

    var track = noteSpawner.CurrentSongData.tracks
        .Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"));
    if (track != null)
    {
        // Ищем именно ту Star Power-ноту, которая максимально близка к моменту нажатия
        var note = track.notes
            .Where(n => n.midi == midi && n.isStarPower && Math.Abs(n.time - (Time.time - noteSpawner.StartTime) + noteOffset) < starPowerDetectionWindow)
            .OrderBy(n => Math.Abs(n.time - (Time.time - noteSpawner.StartTime) + noteOffset))
            .FirstOrDefault();
        if (note != null)
        {
            int sectionIndex = ChartParser.StarPowerSections.FindIndex(sp => 
                note.tick >= sp.startTick && note.tick <= sp.startTick + sp.duration);
            if (sectionIndex >= 0)
            {
                if (!processedTicksPerSection.ContainsKey(sectionIndex))
                {
                    processedTicksPerSection[sectionIndex] = new HashSet<int>();
                }
                processedTicksPerSection[sectionIndex].Add(note.tick);
                starPowerSectionSuccess[sectionIndex] = true;

                // Для короткой ноты (не длинной) проверяем завершение секции
                if (!isLongNote)
                {
                    var sectionNotes = track.notes
                        .Where(n => n.tick >= ChartParser.StarPowerSections[sectionIndex].startTick && 
                                   n.tick <= ChartParser.StarPowerSections[sectionIndex].startTick + ChartParser.StarPowerSections[sectionIndex].duration && 
                                   n.isStarPower)
                        .ToList();
                    bool allNotesProcessed = sectionNotes.All(n => processedTicksPerSection[sectionIndex].Contains(n.tick));
                    if (allNotesProcessed && !sectionEnergyAwarded.Contains(sectionIndex))
                    {
                        StartCoroutine(SmoothAddStarPowerEnergy(0.25f, 0.2f));
                        sectionEnergyAwarded.Add(sectionIndex);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"No Star Power section found for note tick={note.tick}, midi={midi}");
            }
        }
    }
    else
    {
        Debug.LogWarning("Track not found for selected difficulty");
    }

    UpdateUI();
}

private void HandleNoteSustainEnd(int midi)
{
    var noteObj = noteSpawner.SpawnedObjects.FirstOrDefault(obj => 
        obj.GetComponent<NoteController>()?.Midi == midi && 
        obj.GetComponent<NoteController>().Duration > 0.1f);
    if (noteObj == null) return;

    float noteTime = noteSpawner.baseObjectTimes[noteObj];
    float currentTime = Time.time - noteSpawner.StartTime;
    var noteData = noteSpawner.CurrentSongData.tracks
        .Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"))
        ?.notes
        .Where(n => Math.Abs(n.time - noteTime + noteOffset) < 0.001f && n.midi == midi && currentTime >= n.time && currentTime <= n.time + n.duration + 0.1f)
        .OrderBy(n => Math.Abs(n.time - noteTime + noteOffset))
        .FirstOrDefault();
    if (noteData == null || !noteData.isStarPower)
    {
        return;
    }

    int sectionIndex = ChartParser.StarPowerSections.FindIndex(sp => 
        noteData.tick >= sp.startTick && 
        noteData.tick <= sp.startTick + sp.duration);
    if (sectionIndex >= 0)
    {
        var spSection = ChartParser.StarPowerSections[sectionIndex];

        if (!processedTicksPerSection.ContainsKey(sectionIndex))
        {
            processedTicksPerSection[sectionIndex] = new HashSet<int>();
        }
        processedTicksPerSection[sectionIndex].Add(noteData.tick);

        // Проверяем Star Power ноты в секции
        var sectionNotes = noteSpawner.CurrentSongData.tracks
            .Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"))
            ?.notes
            .Where(n => n.tick >= spSection.startTick && n.tick <= spSection.startTick + spSection.duration && n.isStarPower)
            .ToList();
        var uniqueTicks = sectionNotes?.Select(n => n.tick).Distinct().ToList() ?? new List<int>();
        bool allTicksProcessed = uniqueTicks.All(tick => processedTicksPerSection[sectionIndex].Contains(tick));

        // Проверяем, является ли текущая нота последней Star Power в секции
        var lastStarPowerNote = sectionNotes
            .OrderByDescending(n => n.tick + (n.duration * ChartParser.GetBPMAtTick(n.tick, noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq) / 60f * noteSpawner.CurrentSongData.header.ppq))
            .FirstOrDefault();
        if (lastStarPowerNote != null && lastStarPowerNote.midi == midi && lastStarPowerNote.tick == noteData.tick && starPowerSectionSuccess.ContainsKey(sectionIndex) && starPowerSectionSuccess[sectionIndex] && allTicksProcessed && !sectionEnergyAwarded.Contains(sectionIndex))
        {
            StartCoroutine(SmoothAddStarPowerEnergy(0.25f, 0.2f));
            sectionEnergyAwarded.Add(sectionIndex);
        }
    }
    else
    {
        //Debug.Log($"No Star Power section found for Star Power note midi={midi} at tick={noteData?.tick}, time={noteTime:F4}s, currentTime={currentTime:F4}s");
    }
}

    private void HandleNoteSustainTick(int midi, int points)
    {
        AddScore(points);
        UpdateUI();
    }
// Изменить метод CheckStarPowerSections
private void CheckStarPowerSections()
{
    float currentTime = noteSpawner.AudioSource.time;
    HashSet<int> processedSectionsThisFrame = new HashSet<int>();
    for (int i = 0; i < ChartParser.StarPowerSections.Count; i++)
    {
        if (processedSectionsThisFrame.Contains(i) || sectionEnergyAwarded.Contains(i)) continue;

        var spSection = ChartParser.StarPowerSections[i];
        bool isZeroLengthSection = spSection.duration == 0;
        bool isLastNoteLong = false;
        float lastNoteDuration = 0f;
        // Выбираем последнюю Star Power ноту в секции
        var lastNote = noteSpawner.CurrentSongData.tracks
            .Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"))
            ?.notes
            .Where(n => n.tick >= spSection.startTick && n.tick <= spSection.startTick + spSection.duration && n.isStarPower)
            .OrderByDescending(n => n.tick + (n.duration * ChartParser.GetBPMAtTick(n.tick, noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq) / 60f * noteSpawner.CurrentSongData.header.ppq))
            .FirstOrDefault();
        float lastStarPowerNoteEndTime = spSection.endTime; // Запасной вариант: конец секции
        if (lastNote != null)
        {
            lastNoteDuration = lastNote.duration;
            isLastNoteLong = lastNoteDuration > 0.1f;
            float bpm = ChartParser.GetBPMAtTick(lastNote.tick, noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq);
            lastStarPowerNoteEndTime = ChartParser.CalculateNoteTime(lastNote.tick + (int)(lastNote.duration * noteSpawner.CurrentSongData.header.ppq / (60f / bpm)), noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq);
        }

        // Собираем только Star Power ноты в секции
        var sectionNotes = noteSpawner.CurrentSongData.tracks
            .Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"))
            ?.notes
            .Where(n => n.tick >= spSection.startTick && n.tick <= spSection.startTick + spSection.duration && n.isStarPower)
            .ToList();
        var uniqueTicks = sectionNotes?.Select(n => n.tick).Distinct().ToList() ?? new List<int>();

        // Инициализируем processedTicksPerSection для секции, если ещё не существует
        if (!processedTicksPerSection.ContainsKey(i))
        {
            processedTicksPerSection[i] = new HashSet<int>();
        }

        // Проверяем, все ли уникальные тики Star Power нот в секции обработаны
        bool allTicksProcessed = uniqueTicks.All(tick => processedTicksPerSection[i].Contains(tick));

        // Проверяем, завершена ли длинная нота
        bool isLongNoteCompleted = !isLastNoteLong;
        if (isLastNoteLong && allTicksProcessed)
        {
            var noteObj = noteSpawner.SpawnedObjects.FirstOrDefault(obj => 
                obj.GetComponent<NoteController>()?.Duration > 0.1f && 
                Math.Abs(noteSpawner.baseObjectTimes[obj] - spSection.startTime - noteOffset) < 0.001f);
            if (noteObj != null)
            {
                var noteController = noteObj.GetComponent<NoteController>();
                isLongNoteCompleted = !noteController.IsSustained;
            }
            else
            {
                isLongNoteCompleted = true; // Если нота не найдена, считаем завершённой
            }
        }

        // Проверяем, были ли Star Power ноты в секции и сыграна ли хотя бы одна
        bool hasStarPowerNotes = sectionNotes != null && sectionNotes.Any();
        bool hasProcessedNotes = processedTicksPerSection[i].Any();

        // Начисляем энергию, только если есть Star Power ноты и хотя бы одна сыграна
        if (hasStarPowerNotes && hasProcessedNotes && allTicksProcessed && currentTime >= lastStarPowerNoteEndTime && isLongNoteCompleted && !sectionEnergyAwarded.Contains(i) && !isAddingEnergy)
        {
            StartCoroutine(SmoothAddStarPowerEnergy(0.25f, 0.2f));
            starPowerSectionSuccess.Remove(i);
            processedTicksPerSection.Remove(i);
            processedSectionsThisFrame.Add(i);
            sectionEnergyAwarded.Add(i);
        }
    }
}
// Заменить метод UpdateStarPowerUI
private void UpdateStarPowerUI()
{
    if (starPowerModel != null)
    {
        Transform speTransform = starPowerModel.transform.Find("SPEnergy");
        if (speTransform != null)
        {
            if (starPowerEnergy > 0f)
            {
                speTransform.gameObject.SetActive(true);
                float newScaleX = starPowerEnergy * 1f;
                // Смещаем позицию, чтобы левый край оставался на initialXPosition
                float newPositionX = -2.37f + (newScaleX * 2.37f);
                speTransform.localScale = new Vector3(newScaleX, initialSPEnergyScale.y, initialSPEnergyScale.z);
                speTransform.localPosition = new Vector3(newPositionX, initialSPEnergyPosition.y, initialSPEnergyPosition.z);
            }
            else
            {
                speTransform.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("SPEnergy mesh not found in SP2 model!");
        }
    }
    else
    {
        Debug.LogWarning("starPowerModel is not assigned!");
    }
}

// Добавить новый метод SmoothAddStarPowerEnergy
private IEnumerator SmoothAddStarPowerEnergy(float energyIncrement, float duration)
{
    if (isAddingEnergy)
    {
        yield return new WaitUntil(() => !isAddingEnergy);
    }
    isAddingEnergy = true;

    float startEnergy = starPowerEnergy;
    float targetEnergy = Mathf.Min(1f, starPowerEnergy + energyIncrement);
    float elapsed = 0f;

    // Проигрываем звук добавления энергии, если это не Whammy
    AudioSource audioSource = GetComponent<AudioSource>();
    if (audioSource != null && starPowerEnergyAddClip != null && whammyEnergyCoroutine == null)
    {
        audioSource.PlayOneShot(starPowerEnergyAddClip);
    }
    else if (audioSource == null)
    {
        Debug.LogWarning("Cannot play starPowerEnergyAddClip: AudioSource component not found!");
    }
    else if (starPowerEnergyAddClip == null)
    {
        Debug.LogWarning("Cannot play starPowerEnergyAddClip: clip is null!");
    }

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        starPowerEnergy = Mathf.Lerp(startEnergy, targetEnergy, t);
        UpdateStarPowerUI();
        yield return null;
    }

    starPowerEnergy = targetEnergy;
    UpdateStarPowerUI();

    isAddingEnergy = false;
}

private IEnumerator SmoothAddWhammyEnergy(GameObject noteObj, NoteController noteController, NoteData noteData)
{
    float elapsed = 0f;
    float timeHeld = 0f;
    float startEnergy = starPowerEnergy;
    float maxDuration = 0.01f; // Минимальное время удержания Whammy для получения maxEnergyIncrement
    float maxEnergyIncrement = 0.01f; // 1% энергии за удержание
    float animationDuration = 0.1f; // Фиксированное время анимации
    bool isWhammyActive = true;

    while (elapsed < animationDuration)
    {
        if (isWhammyActive && (noteObj == null || !noteController.IsSustained || !InputManager.Instance.IsKey("Whammy") || (Time.time - noteSpawner.StartTime) > noteData.time + noteData.duration))
        {
            isWhammyActive = false;
            float finalEnergyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement;
            float finalTargetEnergy = Mathf.Min(1f, startEnergy + finalEnergyIncrement);
            starPowerEnergy = Mathf.Max(starPowerEnergy, finalTargetEnergy);
            UpdateStarPowerUI();
        }

        elapsed += Time.deltaTime;
        if (isWhammyActive)
        {
            timeHeld = Mathf.Min(timeHeld + Time.deltaTime, maxDuration);
        }

        float t = elapsed / animationDuration;
       // t = t * t * (3f - 2f * t); // SmoothStep
        float energyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement * t;
        float newEnergyValue = Mathf.Min(1f, startEnergy + energyIncrement);
        starPowerEnergy = Mathf.Max(starPowerEnergy, newEnergyValue);
        UpdateStarPowerUI();
        yield return null;
    }

    float completedEnergyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement;
    float completedTargetEnergy = Mathf.Min(1f, startEnergy + completedEnergyIncrement);
    starPowerEnergy = Mathf.Max(starPowerEnergy, completedTargetEnergy);
    UpdateStarPowerUI();
    whammyEnergyCoroutine = null;
}
private IEnumerator SmoothConsumeStarPowerEnergy()
{
    float currentTime = noteSpawner.AudioSource.time;
    float bpm = ChartParser.GetBPMAtTick(currentTime * noteSpawner.CurrentSongData.header.ppq, noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq);
    float beatDuration = 60f / bpm; // Длительность одного бита в секундах
    float depletionRate = 1f / (28f * beatDuration); // 1.0 энергии за 28 битов
    float lastEnergy = starPowerEnergy;
    float elapsedBeats = 0f;

    while (starPowerEnergy > 0f)
    {
        float elapsed = Time.deltaTime;
        elapsedBeats += elapsed / beatDuration;

        if (starPowerEnergy != lastEnergy)
        {
            // Добавляем время пропорционально новой энергии
            float deltaEnergy = starPowerEnergy - lastEnergy;
            float additionalBeats = deltaEnergy * 28f;
            elapsedBeats -= additionalBeats; // Компенсируем добавленную энергию
            lastEnergy = starPowerEnergy;
        }

        starPowerEnergy = Mathf.Max(0f, starPowerEnergy - depletionRate * elapsed);
        float beatsRemaining = starPowerEnergy * 28f; // Оставшиеся биты
        UpdateStarPowerUI();
        yield return null;
    }

    starPowerEnergy = 0f;
    isStarPowerActive = false;
    starPowerCoroutine = null;
    int baseMultiplier = Mathf.Max(1, Mathf.Min(maxMultiplierLimit, 1 + streak / 10));
    multiplier = baseMultiplier;
    if (starPowerEffect != null)
    {
        starPowerEffect.SetActive(false);
    }
    UpdateMultiplier();
    UpdateStarPowerUI();
    UpdateUI();
}


private void HandleWhammyInput()
{
    float currentTime = Time.time - noteSpawner.StartTime;
    var track = noteSpawner.CurrentSongData?.tracks.Find(t => t.name == PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle"));
    if (track == null)
    {
        if (whammyEnergyCoroutine != null)
        {
            StopCoroutine(whammyEnergyCoroutine);
            whammyEnergyCoroutine = null;
            Debug.Log("Stopped Whammy energy coroutine due to missing track");
        }
        canStartNewWhammyCycle = true;
        return;
    }

    bool isWhammyHeld = InputManager.Instance.IsKey("Whammy");
    bool isWhammyReleased = InputManager.Instance.IsKeyUp("Whammy");
    GameObject activeNoteObj = null;
    NoteController activeNoteController = null;
    NoteData activeNoteData = null;

    if (isWhammyHeld)
    {
        foreach (var noteObj in noteSpawner.SpawnedObjects.ToList())
        {
            if (noteObj == null) continue;
            var noteController = noteObj.GetComponent<NoteController>();
            if (noteController != null && noteController.IsStarPower && noteController.IsSustained && noteController.Duration > 0.1f)
            {
                float noteTime = noteSpawner.baseObjectTimes[noteObj];
                var noteData = track.notes
                    .Where(n => n.midi == noteController.Midi && n.isStarPower && Math.Abs(n.time - noteTime + noteOffset) < starPowerDetectionWindow && currentTime >= n.time && currentTime <= n.time + n.duration)
                    .OrderBy(n => Math.Abs(n.time - noteTime + noteOffset))
                    .FirstOrDefault();
                if (noteData != null)
                {
                    activeNoteObj = noteObj;
                    activeNoteController = noteController;
                    activeNoteData = noteData;
                    break;
                }
            }
        }
    }

    if (isWhammyReleased)
    {
        canStartNewWhammyCycle = true;
        if (whammyEnergyCoroutine != null)
        {
            StopCoroutine(whammyEnergyCoroutine);
            whammyEnergyCoroutine = null;
        }
    }

    if (isWhammyHeld && activeNoteObj != null && canStartNewWhammyCycle && whammyEnergyCoroutine == null && !isAddingEnergy)
    {
        whammyEnergyCoroutine = StartCoroutine(SmoothAddWhammyEnergy(activeNoteObj, activeNoteController, activeNoteData));
        canStartNewWhammyCycle = false;
    }
    else if (isWhammyHeld && activeNoteObj != null && whammyEnergyCoroutine == null)
    {
        Debug.Log($"Cannot start Whammy energy coroutine: canStartNewWhammyCycle={canStartNewWhammyCycle}, currentTime={currentTime:F4}s");
    }
    else if (whammyEnergyCoroutine != null && (!isWhammyHeld || activeNoteObj == null))
    {
        StopCoroutine(whammyEnergyCoroutine);
        whammyEnergyCoroutine = null;
    }
}

private void Update()
{
    if (noteSpawner.SpawnedObjects.Count == 0 && !statsPanel.activeSelf) return;

    processedChordsThisFrame.Clear();
    processedStarPowerChordsThisFrame.Clear();
    missedChordsThisFrame.Clear();
    bool missedChordThisFrame = false;
    foreach (var noteObj in noteSpawner.SpawnedObjects.ToList())
    {
        if (noteObj == null) continue;
        NoteController note = noteObj.GetComponent<NoteController>();
        if (note != null && note.IsActive && note.transform.position.z < -noteInputManager.HitZoneTolerance)
        {
            int chordId = Time.frameCount;
            if (!missedNotesSet.Contains(noteObj))
            {
                missedNotesSet.Add(noteObj);
                if (!missedChordsThisFrame.Contains(chordId))
                {
                    missedChordsThisFrame.Add(chordId);
                    missedChordThisFrame = true;
                }
            }
        }
    }
    if (missedChordThisFrame)
    {
        foreach (var noteObj in noteSpawner.SpawnedObjects.ToList())
        {
            if (noteObj == null) continue;
            NoteController note = noteObj.GetComponent<NoteController>();
            if (note != null && note.IsActive && note.IsStarPower && note.transform.position.z < -noteInputManager.HitZoneTolerance)
            {
                int noteId = noteObj.GetInstanceID();
                if (!processedStarPowerNotes.Contains(noteId))
                {
                    processedStarPowerNotes.Add(noteId);
                    if (noteSpawner.baseObjectTimes.ContainsKey(noteObj))
                    {
                        float noteTime = noteSpawner.baseObjectTimes[noteObj];
                        int sectionIndex = ChartParser.StarPowerSections.FindIndex(sp => noteTime >= sp.startTime && noteTime < sp.endTime);
                        if (sectionIndex >= 0)
                        {
                            starPowerSectionSuccess[sectionIndex] = false;
                            Debug.Log($"Missed Star Power note at time={noteTime:F2}s, sectionIndex={sectionIndex}. Starting ReplaceStarPowerNotesInSection.");
                            StartCoroutine(noteSpawner.ReplaceStarPowerNotesInSection(sectionIndex));
                        }
                        else
                        {
                            Debug.LogWarning($"No Star Power section found for note at time={noteTime:F2}s");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Note object {noteObj.name} (ID={noteId}) not found in baseObjectTimes");
                    }
                }
            }
        }
        missedNotes++;
        ResetStreak();
        UpdateUI();
    }

    var toRemove = new List<GameObject>();
    foreach (var note in missedNotesSet)
    {
        if (note == null || !noteSpawner.SpawnedObjects.Contains(note))
        {
            toRemove.Add(note);
        }
    }
    foreach (var note in toRemove)
    {
        missedNotesSet.Remove(note);
    }

    if (noteSpawner.AudioSource.isPlaying)
    {
        hasStartedPlaying = true;
    }

    if (InputManager.Instance.IsKeyDown("StarPower") && starPowerEnergy > 0.25f && starPowerCoroutine == null && !isStarPowerActive)
    {
        starPowerCoroutine = StartCoroutine(SmoothConsumeStarPowerEnergy());
        isStarPowerActive = true;
        if (starPowerEffect != null)
        {
            starPowerEffect.SetActive(true);
        }
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null && starPowerActivateClip != null)
        {
            audioSource.PlayOneShot(starPowerActivateClip);
        }
        else if (audioSource == null)
        {
            Debug.LogWarning("Cannot play starPowerActivateClip: AudioSource component not found!");
        }
        else if (starPowerActivateClip == null)
        {
            Debug.LogWarning("Cannot play starPowerActivateClip: clip is null!");
        }
        UpdateMultiplier();
        UpdateUI();
    }

    HandleWhammyInput();
    CheckStarPowerSections();
    if (statsPanel.activeSelf)
    {
        if (InputManager.Instance.IsKeyDown("Green"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("SongSelect");
        }
        else if (InputManager.Instance.IsKeyDown("Red"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}

private void AddScore(int points)
{
    score += points * (isStarPowerActive ? multiplier * 2 : multiplier);
}

private void UpdateMultiplier()
{
    int baseMultiplier = Mathf.Max(1, Mathf.Min(maxMultiplierLimit, 1 + streak / 10));
    multiplier = isStarPowerActive ? baseMultiplier * 2 : baseMultiplier;
}

private void ResetStreak()
{
    streak = 0;
    UpdateMultiplier();
}

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"{score.ToString("N0").Replace(",", " ")}";
        if (multiplierText != null) multiplierText.text = $"x{multiplier}";
        if (streakText != null) streakText.text = $"{streak}";
        UpdateStarPowerUI();
    }

    private void CalculateTotalNotes()
    {
        if (noteSpawner.CurrentSongData == null || noteSpawner.CurrentSongData.tracks == null)
        {
            Debug.LogError("Song data or tracks are null!");
            totalNotes = 0;
            return;
        }

string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");

var track = noteSpawner.CurrentSongData.tracks.Find(t => t.name == selectedDifficulty);
if (track == null || track.notes == null || track.notes.Count == 0)
{
    Debug.LogError($"Track for difficulty {selectedDifficulty} not found or has no notes!");
    totalNotes = 0;
    return;
}

        HashSet<float> chordTimes = new HashSet<float>();
        foreach (var note in track.notes)
        {
            chordTimes.Add(note.time);
        }

        totalNotes = chordTimes.Count;
    }




    private void HandleCalibrationOffset(float offsetMs)
    {
        if (!IsCalibrationActive() || !IsFirstStage())
        {
            return;
        }

        bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (showAccuracy)
        {
            if (offsetText == null)
            {
                Debug.LogError("offsetText is null, cannot show offset!");
                return;
            }
            StartCoroutine(ShowCalibrationOffset(offsetMs));
        }
        else
        {
            Debug.LogWarning("showAccuracy is false, offset text not shown");
        }
    }

    private IEnumerator ShowCalibrationOffset(float offsetMs)
    {
        if (lastOffsetInstance != null)
        {
            StartCoroutine(FadeOutAccuracy(lastOffsetInstance));
        }

        if (offsetText == null)
        {
            Debug.LogError("offsetText is null, cannot instantiate!");
            yield break;
        }

        GameObject offsetInstance = Instantiate(offsetText.gameObject, initialOffsetPosition, Quaternion.identity, offsetText.transform.parent);
        Text offsetTextInstance = offsetInstance.GetComponent<Text>();
        if (offsetTextInstance == null)
        {
            Destroy(offsetInstance);
            yield break;
        }

        lastOffsetInstance = offsetInstance;
        offsetTextInstance.transform.position = initialOffsetPosition;

        int offsetInt = Mathf.RoundToInt(offsetMs);
        string offsetTextStr = offsetInt == 0 ? "0" : (offsetInt > 0 ? $"+{offsetInt}" : $"{offsetInt}");
        offsetTextInstance.text = offsetTextStr;

        Vector3 startPos = initialOffsetPosition;
        Vector3 showPos = startPos + Vector3.right * 15f;
        Vector3 endPos = showPos + Vector3.right * 35f;

        float showDuration = 0.1f;
        float elapsed = 0f;
        while (elapsed < showDuration)
        {
            if (offsetInstance == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / showDuration;
            offsetTextInstance.transform.position = Vector3.Lerp(startPos, showPos, t);
            yield return null;
        }

        float slideDuration = 1.5f;
        float fadeStartTime = slideDuration - 1f;
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (offsetInstance == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            offsetTextInstance.transform.position = Vector3.Lerp(showPos, endPos, t);

            if (elapsed >= fadeStartTime)
            {
                float fadeT = (elapsed - fadeStartTime) / 1f;
                float alpha = 1f - fadeT;
                offsetTextInstance.color = new Color(offsetTextInstance.color.r, offsetTextInstance.color.g, offsetTextInstance.color.b, alpha);
            }
            yield return null;
        }

        if (offsetInstance != null)
        {
            Destroy(offsetInstance);
            if (lastOffsetInstance == offsetInstance) lastOffsetInstance = null;
        }
    }

private IEnumerator ShowAccuracy(NoteInputManager.HitAccuracy accuracy)
{
    // Если есть предыдущий текст, запускаем его исчезновение
    if (lastAccuracyInstance != null)
    {
        StartCoroutine(FadeOutAccuracy(lastAccuracyInstance));
    }

    // Создаём копию текста из сцены
    GameObject accuracyInstance = Instantiate(accuracyText.gameObject, initialAccuracyPosition, Quaternion.identity, accuracyText.transform.parent);
    Text accuracyTextInstance = accuracyInstance.GetComponent<Text>();

    // Сохраняем новый экземпляр как последний
    lastAccuracyInstance = accuracyInstance;

    // Сбрасываем позицию
    accuracyTextInstance.transform.position = initialAccuracyPosition;

    // Настраиваем текст и цвет
    switch (accuracy)
    {
        case NoteInputManager.HitAccuracy.Perfect:
            accuracyTextInstance.text = "PERFECT";
            //accuracyTextInstance.color = new Color(0, 1, 0, 0); // Зелёный, прозрачный изначально
            break;
        case NoteInputManager.HitAccuracy.Early:
            accuracyTextInstance.text = "EARLY";
            //accuracyTextInstance.color = new Color(1, 1, 0, 0); // Жёлтый, прозрачный изначально
            break;
        case NoteInputManager.HitAccuracy.Late:
            accuracyTextInstance.text = "LATE";
            //accuracyTextInstance.color = new Color(1, 0, 0, 0); // Красный, прозрачный изначально
            break;
    }

    // Начальная позиция текста
    Vector3 startPos = initialAccuracyPosition;
    Vector3 showPos = startPos + Vector3.right * 15f; // Выдвигается на 5 вправо
    Vector3 endPos = showPos + Vector3.right * 35f;   // Конечная позиция через 5 секунд

    // Появление (0.1 секунды)
    float showDuration = 0.1f;
    float elapsed = 0f;
    while (elapsed < showDuration)
    {
        if (accuracyInstance == null) yield break;
        elapsed += Time.deltaTime;
        float t = elapsed / showDuration;
        accuracyTextInstance.transform.position = Vector3.Lerp(startPos, showPos, t);
        accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, t);
        yield return null;
    }

    float slideDuration = 1.5f; // Скольжение с исчезновением (5 секунд всего, исчезновение на последние 0.3 секунды)
    float fadeStartTime = slideDuration - 1f; // Начинаем исчезновение за 0.3 сек до конца
    elapsed = 0f;
    while (elapsed < slideDuration)
    {
        if (accuracyInstance == null) yield break;
        elapsed += Time.deltaTime;
        float t = elapsed / slideDuration;
        accuracyTextInstance.transform.position = Vector3.Lerp(showPos, endPos, t);

        // Исчезновение начинается на 4.7 секунде
        if (elapsed >= fadeStartTime)
        {
            float fadeT = (elapsed - fadeStartTime) / 1f; // Прогресс исчезновения (0 → 1 за 0.3 сек)
            float alpha = 1f - fadeT; // От 1 до 0
            accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, alpha);
        }
        yield return null;
    }

    // Уничтожаем копию текста
    if (accuracyInstance != null)
    {
        Destroy(accuracyInstance);
        if (lastAccuracyInstance == accuracyInstance) lastAccuracyInstance = null;
    }
}

private IEnumerator FadeOutAccuracy(GameObject instance)
{
    if (instance == null) yield break;

    Text accuracyTextInstance = instance.GetComponent<Text>();
    float fadeDuration = 0.1f; // Быстрое исчезновение за 0.3 секунды
    float elapsed = 0f;

    while (elapsed < fadeDuration)
    {
        if (instance == null) yield break;
        elapsed += Time.deltaTime;
        float t = 1f - (elapsed / fadeDuration);
        accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, t);
        yield return null;
    }

    if (instance != null) Destroy(instance);
}


private IEnumerator CheckSongEnd()
{
    yield return new WaitUntil(() => hasStartedPlaying && noteSpawner.AudioSource.isPlaying);
    yield return new WaitUntil(() => !noteSpawner.AudioSource.isPlaying && noteSpawner.SpawnedObjects.Count == 0);

    bool isCalibration = PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    if (isCalibration)
    {
        CalibrationManager.Instance.HandleSongEnd();
    }
    else
    {
        ShowStats();
    }
}

    private void ShowStats()
    {
        statsPanel.SetActive(true);
        Time.timeScale = 0f;
        float hitPercentage = totalNotes > 0 ? Mathf.Floor((float)hitNotes / totalNotes * 100f) : 0f;

        statsScoreText.text = $"{score.ToString("N0").Replace(",", " ")}";
        statsProgressText.text = $"{hitPercentage}%";
        string rank = GetRank(hitPercentage);
        string starsDisplay = score > 0 ? new string('★', int.Parse(rank)) : "-";
        statsStarsText.text = score > 0 ? (hitPercentage >= 100f ? $"<color=#ffa200>{starsDisplay}</color>" : starsDisplay) : "-";
        statsTotalNotesText.text = $"{totalNotes}";
        statsMaxStreakText.text = $"{maxStreak}";
        statsHitNotesText.text = $"{hitNotes}";
        statsMissedNotesText.text = $"{missedNotes}";

        // Предыдущие параметры
        string songName = PlayerPrefs.GetString("SelectedSong", "");
        string difficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        string keyPrefix = $"{songName}_{difficulty}";
        int previousScore = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_Score", 0);
        if (previousScore > 0)
        {
            //float previousProgress = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Progress", 0f);
            //string previousRank = UserManager.Instance.GetUserProgressString($"{keyPrefix}_Stars", "0");
            int previousMaxStreak = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_MaxStreak", 0);
            int previousHitNotes = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_HitNotes", 0);
            //int previousMissedNotes = previousTotalNotes - previousHitNotes;
            //float previousAccuracy = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Accuracy", -1f);
            //int previousPerfectHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_PerfectHits", 0);
            //int previousEarlyHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_EarlyHits", 0);
            //int previousLateHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_LateHits", 0);

            statsPreviousScoreText.text = $"{previousScore.ToString("N0").Replace(",", " ")}";
            //statsPreviousProgressText.text = $"Previous Progress: {previousProgress}%";
            //string previousStarsDisplay = new string('★', int.Parse(previousRank));
            //statsPreviousStarsText.text = previousProgress >= 100f ? $"<color=#ffa200>{previousStarsDisplay}</color>" : previousStarsDisplay;
            statsPreviousMaxStreakText.text = $"{previousMaxStreak}";
            statsPreviousHitNotesText.text = $"{previousHitNotes}";
            //statsPreviousMissedNotesText.text = $"Previous Missed: {previousMissedNotes}";
            //statsPreviousAccuracyText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Accuracy: {previousAccuracy:F0}%" : "Previous Accuracy: N/A";
            //statsPreviousPerfectText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Perfect: {previousPerfectHits}" : "Previous Perfect: N/A";
            //statsPreviousEarlyText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Early: {previousEarlyHits}" : "Previous Early: N/A";
            //statsPreviousLateText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Late: {previousLateHits}" : "Previous Late: N/A";
        }
        else
        {
            statsPreviousScoreText.text = "-";
            //statsPreviousProgressText.text = "Previous Progress: -";
            //statsPreviousStarsText.text = "Previous Stars: -";
            statsPreviousMaxStreakText.text = "-";
            statsPreviousHitNotesText.text = "-";
            //statsPreviousMissedNotesText.text = "Previous Missed: -";
            //statsPreviousAccuracyText.text = "Previous Accuracy: -";
            //statsPreviousPerfectText.text = "Previous Perfect: -";
            //statsPreviousEarlyText.text = "Previous Early: -";
            //statsPreviousLateText.text = "Previous Late: -";
        }

        // Обновляем UI песни на statsPanel
        if (statsBandNameText != null)
        {
            statsBandNameText.text = PlayerPrefs.GetString("BandName", "Unknown Artist");
        }

        if (statsSongTitleText != null)
        {
            statsSongTitleText.text = PlayerPrefs.GetString("SongTitle", "Unknown Song");
        }

        if (statsSongCharterText != null)
        {
            string charter = PlayerPrefs.GetString("SongCharter", "Unknown Charter");
            statsSongCharterText.text = string.IsNullOrEmpty(charter) || charter == "Unknown Charter" ? "Unknown Charter" : $"By {charter}";
        }

        if (statsSongCoverImage != null)
        {
            string coverPath = PlayerPrefs.GetString("CoverPath", "default");
            if (coverPath != "default" && File.Exists(coverPath))
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                statsSongCoverImage.sprite = sprite;
            }
            else if (defaultCover != null)
            {
                statsSongCoverImage.sprite = defaultCover;
            }
        }


        float accuracyPercent = 0f;
        bool hasSavedAccuracy = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Accuracy", -1f) >= 0f;
        if (useAccuracySystem)
        {
            accuracyPercent = hitNotes > 0 ? (perfectHits + 0.5f * (earlyHits + lateHits)) / hitNotes * 100f : 0f;
        }

        statsAccuracyText.text = useAccuracySystem ? $"{accuracyPercent:F0}%" : "N/A";
        statsPerfectText.text = useAccuracySystem ? $"Perfect: {perfectHits}" : "Perfect: N/A";
        statsEarlyText.text = useAccuracySystem ? $"Early: {earlyHits}" : "Early: N/A";
        statsLateText.text = useAccuracySystem ? $"Late: {lateHits}" : "Late: N/A";

        // Используем UserManager для сохранения прогресса
        if (score > previousScore)
        {
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Score", score);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Stars", rank);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Progress", hitPercentage);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_MaxStreak", maxStreak);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_HitNotes", hitNotes);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_MissedNotes", missedNotes);
            if (useAccuracySystem)
            {
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_Accuracy", accuracyPercent);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_PerfectHits", perfectHits);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_EarlyHits", earlyHits);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_LateHits", lateHits);
            }
            else
            {
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_Accuracy", -1f);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_PerfectHits", 0);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_EarlyHits", 0);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_LateHits", 0);
            }
        }
    }

    private void OnDestroy()
    {
        if (noteInputManager != null)
        {
            noteInputManager.OnNoteHit -= HandleNoteHit;
            noteInputManager.OnNoteSustainEnd -= HandleNoteSustainEnd;
            noteInputManager.OnNoteSustainTick -= HandleNoteSustainTick;
        }
        OnCalibrationHit -= HandleCalibrationHit;
        Debug.Log("ScoreManager destroyed, OnCalibrationHit unsubscribed");
    }

    private string GetRank(float hitPercentage)
    {
        if (hitPercentage >= 90f) return "5";
        if (hitPercentage >= 70f) return "4";
        if (hitPercentage >= 50f) return "3";
        if (hitPercentage >= 30f) return "2";
        return "1";
    }
}