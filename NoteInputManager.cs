using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class NoteInputManager : MonoBehaviour
{
    private NoteSpawner noteSpawner;
    public KeyCode[] noteKeys;
    private int buttonCount;
    private readonly int[] noteMidis = { 96, 97, 98, 99, 100 };
    public bool leftyFlip { get; private set; }
    private bool useAccuracySystem; // Добавляем для проверки настройки
    public bool UseAccuracySystem => useAccuracySystem;

    [SerializeField] private GameObject HitZone;
    private float hitZoneTolerance;
    public float HitZoneTolerance => hitZoneTolerance;

    private HashSet<int> processedMidiThisFrame;
    private HashSet<NoteController> reducingNotes;

    public delegate void NoteHitHandler(int midi, bool isLongNote, HitAccuracy accuracy);
    public event NoteHitHandler OnNoteHit;

    public delegate void NoteSustainEndHandler(int midi);
    public event NoteSustainEndHandler OnNoteSustainEnd;

    public delegate void NoteSustainTickHandler(int midi, int points);
    public event NoteSustainTickHandler OnNoteSustainTick;

    public delegate void NoteSustainDelegate(int midi);
    public event NoteSustainDelegate OnNoteSustain;

    private List<float> hitTimings; // Для записи времени нажатий

    private Dictionary<int, Material> sustainedNoteMaterials = new Dictionary<int, Material>();

    public enum HitAccuracy
    {
        None,
        Perfect,
        Early,
        Late
    }

    private void Awake()
    {
        string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                           (selectedDifficulty == "EasySingle" || selectedDifficulty == "MediumSingle");
        buttonCount = useFourFrets ? 4 : 5;
        UpdateNoteKeys();
    }

    private void Start()
    {
        noteSpawner = FindObjectOfType<NoteSpawner>();
        if (noteSpawner == null)
        {
            Debug.LogError("NoteSpawner not found in scene!");
            return;
        }

        processedMidiThisFrame = new HashSet<int>();
        reducingNotes = new HashSet<NoteController>();
        leftyFlip = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
        useAccuracySystem = PlayerPrefs.GetInt($"AccuracySystem_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
        hitZoneTolerance = HitZone.transform.lossyScale.z / 2f;
        hitTimings = new List<float>(); // Инициализируем список
    sustainedNoteMaterials = new Dictionary<int, Material>();

        UserManager.Instance.OnUserChanged += OnUserChanged;
    }

    private void OnDestroy()
    {
        if (UserManager.Instance != null)
        {
            UserManager.Instance.OnUserChanged -= OnUserChanged;
        }
    }

    private void UpdateNoteKeys()
    {
        if (buttonCount == 4)
        {
            noteKeys = new KeyCode[]
            {
                InputManager.Instance.GetKey("Green"),
                InputManager.Instance.GetKey("Red"),
                InputManager.Instance.GetKey("Yellow"),
                InputManager.Instance.GetKey("Blue")
            };
        }
        else
        {
            noteKeys = new KeyCode[]
            {
                InputManager.Instance.GetKey("Green"),
                InputManager.Instance.GetKey("Red"),
                InputManager.Instance.GetKey("Yellow"),
                InputManager.Instance.GetKey("Blue"),
                InputManager.Instance.GetKey("Orange")
            };
        }
    }

    private void OnUserChanged()
    {
        UpdateNoteKeys();
        leftyFlip = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
    }

private void Update()
{
    if (noteSpawner.SpawnedObjects.Count == 0) return;

    HashSet<int> pressedKeys = GetPressedKeys();
    bool strumPressed = InputManager.Instance.IsKeyDown("StrumUp") || InputManager.Instance.IsKeyDown("StrumDown"); // Восстанавливаем проверку
    processedMidiThisFrame.Clear();

    List<NoteController> notesInHitZone = new List<NoteController>();
    foreach (var noteObj in noteSpawner.SpawnedObjects)
    {
        if (noteObj == null) continue;
        NoteController note = noteObj.GetComponent<NoteController>();
        if (note != null && note.IsActive && Mathf.Abs(noteObj.transform.position.z) <= hitZoneTolerance)
        {
            notesInHitZone.Add(note);
        }
    }
//    notesInHitZone.Sort((a, b) => Mathf.Abs(a.gameObject.transform.position.z).CompareTo(Mathf.Abs(b.gameObject.transform.position.z)));
notesInHitZone.Sort((a, b) => a.gameObject.transform.position.z.CompareTo(b.gameObject.transform.position.z));
    if (notesInHitZone.Count > 0)
    {
        ProcessNotes(notesInHitZone, pressedKeys, strumPressed);
    }

    // Собираем длинные ноты в отдельный список перед обработкой
    List<NoteController> longNotes = new List<NoteController>();
    foreach (var noteObj in noteSpawner.SpawnedObjects)
    {
        if (noteObj == null) continue;
        NoteController note = noteObj.GetComponent<NoteController>();
        if (note != null && note.IsSustained && note.Duration > 0.1f)
        {
            longNotes.Add(note);
        }
    }

    foreach (var note in longNotes)
    {
        if (note == null || note.gameObject == null) continue; // Пропускаем уничтоженные ноты
        ProcessLongNoteRetention(note, pressedKeys);
    }
}

public HashSet<int> GetPressedKeys()
{
    HashSet<int> pressedKeys = new HashSet<int>();
    for (int i = 0; i < buttonCount; i++)
    {
        if (Input.GetKey(noteKeys[i]))
        {
            pressedKeys.Add(noteMidis[i]);
        }
    }
    return pressedKeys;
}

    private HitAccuracy CalculateAccuracy(float zPosition)
    {
        if (!useAccuracySystem)
        {
            return HitAccuracy.None; // Не вычисляем точность, если система отключена
        }

        if (Mathf.Abs(zPosition) <= 0.3f) return HitAccuracy.Perfect;
        if (zPosition > 0) return HitAccuracy.Early;
        return HitAccuracy.Late;
    }

// Обновляем ProcessNotes
private void ProcessNotes(List<NoteController> notesInHitZone, HashSet<int> pressedKeys, bool strumPressed)
{
    bool isCalibration = PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    bool isFirstStage = isCalibration && PlayerPrefs.GetInt("CalibrationStage", 1) == 1;
    //Debug.Log($"ProcessNotes вызван: isCalibration={isCalibration}, isFirstStage={isFirstStage}, strumPressed={strumPressed}, notesInHitZone.Count={notesInHitZone.Count}");

    if (notesInHitZone.Count == 0) return;

    NoteController closestNote = notesInHitZone[0];
    float closestZ = Mathf.Abs(closestNote.gameObject.transform.position.z);

    List<NoteController> chordNotes = notesInHitZone
        .Where(n => Mathf.Abs(n.gameObject.transform.position.z - closestNote.gameObject.transform.position.z) < 0.01f)
        .ToList();

    bool isChord = chordNotes.Count > 1;

    if (isChord)
    {
        bool allOpenOrSeventh = chordNotes.All(n => n.IsOpen || n.Midi == 103);
        if (allOpenOrSeventh)
        {
            if (strumPressed && pressedKeys.Count == 0)
            {
                foreach (var note in chordNotes)
                {
                    if (!processedMidiThisFrame.Contains(note.Midi))
                    {
                        processedMidiThisFrame.Add(note.Midi);
                        note.Hit();
                        HitAccuracy accuracy = CalculateAccuracy(note.gameObject.transform.position.z);
                        OnNoteHit?.Invoke(note.Midi, note.Duration > 0.1f, accuracy);
                        if (note.Duration <= 0.1f)
                        {
                            noteSpawner.SpawnedObjects.Remove(note.gameObject);
                            Destroy(note.gameObject);
                        }
                    }
                }
            }
        }
        else
        {
            bool allMatch = pressedKeys.Count == chordNotes.Count && chordNotes.All(n => pressedKeys.Contains(n.Midi));
            if ((chordNotes.All(n => n.IsForced) || strumPressed) && allMatch)
            {
                foreach (var note in chordNotes)
                {
                    if (!processedMidiThisFrame.Contains(note.Midi))
                    {
                        processedMidiThisFrame.Add(note.Midi);
                        note.Hit();
                        HitAccuracy accuracy = CalculateAccuracy(note.gameObject.transform.position.z);
                        OnNoteHit?.Invoke(note.Midi, note.Duration > 0.1f, accuracy);
                        if (note.Duration <= 0.1f)
                        {
                            noteSpawner.SpawnedObjects.Remove(note.gameObject);
                            Destroy(note.gameObject);
                        }
                    }
                }
            }
        }
    }
    else
    {
        if (closestNote.IsOpen || closestNote.Midi == 103)
        {
            if (strumPressed && pressedKeys.Count == 0)
            {
                if (!processedMidiThisFrame.Contains(closestNote.Midi))
                {
                    processedMidiThisFrame.Add(closestNote.Midi);
                    closestNote.Hit();
                    HitAccuracy accuracy = CalculateAccuracy(closestNote.gameObject.transform.position.z);
                    OnNoteHit?.Invoke(closestNote.Midi, closestNote.Duration > 0.1f, accuracy);
                    if (closestNote.Duration <= 0.1f)
                    {
                        noteSpawner.SpawnedObjects.Remove(closestNote.gameObject);
                        Destroy(closestNote.gameObject);
                    }
                }
            }
        }
        else if (!processedMidiThisFrame.Contains(closestNote.Midi) && pressedKeys.Contains(closestNote.Midi))
        {
            bool validPress = pressedKeys.Count == 1 || (pressedKeys.Count == 2 && pressedKeys.Except(new[] { closestNote.Midi }).First() < closestNote.Midi);
            if ((closestNote.IsForced || strumPressed) && validPress)
            {
                processedMidiThisFrame.Add(closestNote.Midi);
                closestNote.Hit();
                HitAccuracy accuracy = CalculateAccuracy(closestNote.gameObject.transform.position.z);
                OnNoteHit?.Invoke(closestNote.Midi, closestNote.Duration > 0.1f, accuracy);
                if (closestNote.Duration <= 0.1f)
                {
                    noteSpawner.SpawnedObjects.Remove(closestNote.gameObject);
                    Destroy(closestNote.gameObject);
                }
            }
        }
    }
}

[System.Obsolete("Use CalibrationManager.CalculateAndSaveAudioOffset instead")]
public void CalculateAndSaveAudioOffset()
{
    // Пустой метод для обратной совместимости
}

// Добавить проверку прерывания длинной ноты в ProcessLongNoteRetention
private void ProcessLongNoteRetention(NoteController note, HashSet<int> pressedKeys)
{
    bool allPressed = note.IsOpen || note.Midi == 103
        ? (InputManager.Instance.IsKey("StrumUp") || InputManager.Instance.IsKey("StrumDown")) && pressedKeys.Count == 0
        : pressedKeys.Contains(note.Midi);

    float currentTime = Time.time - noteSpawner.StartTime;
    float noteEndTime = note.TargetTime + note.Duration;

    if (allPressed && note.StickTransform != null)
    {
        OnNoteSustain?.Invoke(note.Midi);
        if (!IsReducingStick(note))
        {
            StartCoroutine(ReduceStick(note));
        }
    }
    else if (note.IsSustained && note.Duration > 0.1f)
    {
        note.IsSustained = false;
        OnNoteSustainEnd?.Invoke(note.Midi);
        Debug.Log($"Long note {note.Midi} interrupted at time={currentTime:F4}s");
    }

    // Проверяем, завершилась ли нота естественно
    if (note.IsSustained && note.Duration > 0.1f && currentTime >= noteEndTime)
    {
        note.IsSustained = false;
        OnNoteSustainEnd?.Invoke(note.Midi);
        Debug.Log($"Long note {note.Midi} completed at time={currentTime:F4}s");
    }
}

    private bool IsReducingStick(NoteController note)
    {
        return reducingNotes.Contains(note);
    }

private IEnumerator ReduceStick(NoteController note)
{
    if (note == null || note.gameObject == null || note.StickTransform == null)
    {
        yield break;
    }

    reducingNotes.Add(note);
    GameObject noteGameObject = note.gameObject;
    Transform stickTransform = note.StickTransform;
    Transform baseTransform = note.BaseTransform;

    float speed = noteSpawner.Speed;
    float hitZoneTolerance = HitZoneTolerance;
    float spawnLeadTime = (noteSpawner.BaseSpawnLeadTime * noteSpawner.GuitarHalfSize) / speed;

    float fullLength = note.Duration * speed;
    float shortNoteZ = noteGameObject.transform.localScale.z * 1.5f;
    float stickLength = fullLength - shortNoteZ;
    float currentZ = noteGameObject.transform.position.z;

    // Подтягиваем ноту к Z=0
    float targetZ = 0f;
    Vector3 newNotePosition = noteGameObject.transform.position;
    newNotePosition.z = targetZ;
    if (noteGameObject != null)
    {
        noteGameObject.transform.position = newNotePosition;
    }
    else
    {
        reducingNotes.Remove(note);
        yield break;
    }

    // Уменьшаем длину палочки и подложки
    float catchOffset = currentZ - targetZ;
    float adjustedStickLength = stickLength + catchOffset;
    if (adjustedStickLength < 0) adjustedStickLength = 0;

    float stickEndZ = targetZ + adjustedStickLength;
    float reducedStickLength = stickEndZ - targetZ;

    float spawnZ = (note.Duration * speed + spawnLeadTime * speed);
    float totalTime = spawnLeadTime + (reducedStickLength / speed) + (hitZoneTolerance / speed);
    float elapsedTimeFromSpawn = (spawnZ - currentZ) / speed;
    float remainingTimeToHitZoneEnd = totalTime - elapsedTimeFromSpawn;
    float totalReductionTime = Mathf.Max(reducedStickLength / speed, remainingTimeToHitZoneEnd);

    Vector3 initialStickScale = stickTransform.localScale;
    Vector3 initialStickPosition = stickTransform.localPosition;
    float stickLossyScaleZ = stickTransform.localScale.z != 0 ? stickTransform.lossyScale.z / stickTransform.localScale.z : 1f;
    initialStickScale.z = reducedStickLength / stickLossyScaleZ;
    if (float.IsNaN(initialStickScale.z) || initialStickScale.z < 0)
    {
        initialStickScale.z = 0;
    }
    initialStickPosition.z = initialStickScale.z / 2f;

    // Устанавливаем масштаб по X для палочки, если нота удерживается
    if (note.IsSustained && note.Midi != 103)
    {
        /*float parentScaleX = noteGameObject.transform.lossyScale.x;
        float targetWorldScaleX = 0.33f;
        float targetScaleX = parentScaleX != 0 ? targetWorldScaleX / parentScaleX : 1f;
        initialStickScale.x = targetScaleX;*/
        Material stickMaterial = stickTransform.GetComponent<Renderer>().material;
        stickMaterial.SetFloat("_GlowEnabled", 1);
        sustainedNoteMaterials[note.Midi] = stickMaterial;
       // Debug.Log($"Note {note.Midi} sustained, Stick Scale X set to: {initialStickScale.x}, World Scale X: {initialStickScale.x * parentScaleX}");
    }
    else if (note.Midi == 103)
    {
        Debug.Log($"Note {note.Midi} sustained, keeping wide Stick Scale X: {initialStickScale.x}, World Scale X: {initialStickScale.x * noteGameObject.transform.lossyScale.x}");
    }

    // Настраиваем начальный масштаб и позицию подложки
    Vector3 initialBaseScale = baseTransform != null ? baseTransform.localScale : Vector3.zero;
    Vector3 initialBasePosition = baseTransform != null ? baseTransform.localPosition : Vector3.zero;
    if (baseTransform != null)
    {
        initialBaseScale.z = reducedStickLength;
        if (float.IsNaN(initialBaseScale.z) || initialBaseScale.z < 0)
        {
            initialBaseScale.z = 0;
        }
        initialBasePosition.z = initialBaseScale.z / 2f;
    }

    if (stickTransform != null)
    {
        if (!float.IsNaN(initialStickScale.x) && !float.IsNaN(initialStickScale.y) && !float.IsNaN(initialStickScale.z))
        {
            stickTransform.localScale = initialStickScale;
        }
        if (!float.IsNaN(initialStickPosition.x) && !float.IsNaN(initialStickPosition.y) && !float.IsNaN(initialStickPosition.z))
        {
            stickTransform.localPosition = initialStickPosition;
        }
    }
    else
    {
        reducingNotes.Remove(note);
        yield break;
    }

    if (baseTransform != null)
    {
        if (!float.IsNaN(initialBaseScale.x) && !float.IsNaN(initialBaseScale.y) && !float.IsNaN(initialBaseScale.z))
        {
            baseTransform.localScale = initialBaseScale;
        }
        if (!float.IsNaN(initialBasePosition.x) && !float.IsNaN(initialBasePosition.y) && !float.IsNaN(initialBasePosition.z))
        {
            baseTransform.localPosition = initialBasePosition;
        }
    }

    float elapsedTime = 0f;
    float sustainTimer = 0f;
    const float sustainInterval = 0.02f;

    while (noteGameObject != null && stickTransform != null && elapsedTime < totalReductionTime && note.IsSustained)
    {
        elapsedTime += Time.deltaTime;
        sustainTimer += Time.deltaTime;

        if (sustainTimer >= sustainInterval)
        {
            OnNoteSustainTick?.Invoke(note.Midi, 1);
            sustainTimer -= sustainInterval;
        }

        float reductionFraction = elapsedTime / totalReductionTime;
        Vector3 newStickScale = initialStickScale;
        Vector3 newStickPosition = initialStickPosition;
        Vector3 newBaseScale = initialBaseScale;
        Vector3 newBasePosition = initialBasePosition;

        newStickScale.z = Mathf.Lerp(initialStickScale.z, 0, reductionFraction);
        if (float.IsNaN(newStickScale.z) || newStickScale.z < 0)
        {
            newStickScale.z = 0;
        }
        newStickPosition.z = newStickScale.z / 2f;

        if (baseTransform != null)
        {
            newBaseScale.z = Mathf.Lerp(initialBaseScale.z, 0, reductionFraction);
            if (float.IsNaN(newBaseScale.z) || newBaseScale.z < 0)
            {
                newBaseScale.z = 0;
            }
            newBasePosition.z = newBaseScale.z / 2f;
        }

        if (stickTransform != null)
        {
            if (!float.IsNaN(newStickScale.x) && !float.IsNaN(newStickScale.y) && !float.IsNaN(newStickScale.z))
            {
                stickTransform.localScale = newStickScale;
            }
            if (!float.IsNaN(newStickPosition.x) && !float.IsNaN(newStickPosition.y) && !float.IsNaN(newStickPosition.z))
            {
                stickTransform.localPosition = newStickPosition;
            }
            else
            {
                break;
            }
        }
        else
        {
            break;
        }

        if (baseTransform != null)
        {
            if (!float.IsNaN(newBaseScale.x) && !float.IsNaN(newBaseScale.y) && !float.IsNaN(newBaseScale.z))
            {
                baseTransform.localScale = newBaseScale;
            }
            if (!float.IsNaN(newBasePosition.x) && !float.IsNaN(newBasePosition.y) && !float.IsNaN(newBasePosition.z))
            {
                baseTransform.localPosition = newBasePosition;
            }
        }

        yield return null;
    }

    // Устанавливаем масштаб палочки в 0 при выходе из цикла
    if (stickTransform != null)
    {
        Vector3 zeroScale = stickTransform.localScale;
        zeroScale.z = 0;
        if (!float.IsNaN(zeroScale.x) && !float.IsNaN(zeroScale.y) && !float.IsNaN(zeroScale.z))
        {
            stickTransform.localScale = zeroScale;
        }
    }

    // Завершаем корутину
    if (noteGameObject != null)
    {
        if (baseTransform != null)
        {
            Vector3 finalBaseScale = baseTransform.localScale;
            if (float.IsNaN(finalBaseScale.z) || finalBaseScale.z < 0)
            {
                finalBaseScale.z = 0;
            }
            baseTransform.localScale = finalBaseScale;

            if (!note.IsSustained && elapsedTime < totalReductionTime && finalBaseScale.z >= 0.5f)
            {
                // Переносим подложку в noteParent и продолжаем движение
                baseTransform.SetParent(noteSpawner.noteParent, true);
                if (noteSpawner.baseObjectCoroutines.ContainsKey(baseTransform.gameObject))
                {
                    noteSpawner.StopCoroutine(noteSpawner.baseObjectCoroutines[baseTransform.gameObject]);
                    noteSpawner.baseObjectCoroutines.Remove(baseTransform.gameObject);
                }
                if (noteSpawner.baseObjectTimes.ContainsKey(baseTransform.gameObject))
                {
                    float adjustedNoteTime = noteSpawner.baseObjectTimes[baseTransform.gameObject];
                    noteSpawner.StartCoroutine(noteSpawner.MoveObjectDown(baseTransform.gameObject, adjustedNoteTime));
                    Debug.Log($"Keeping baseTransform for note {note.Midi} with scale.z={finalBaseScale.z:F4}, elapsedTime={elapsedTime:F4}, totalReductionTime={totalReductionTime:F4}, isSustained={note.IsSustained}, moving to noteParent");
                }
                else
                {
                    Debug.LogWarning($"No baseObjectTimes entry for baseTransform of note {note.Midi}, cannot move");
                    noteSpawner.SpawnedObjects.Remove(baseTransform.gameObject);
                    Destroy(baseTransform.gameObject);
                }
            }
            else
            {
                // Удаляем подложку
                noteSpawner.SpawnedObjects.Remove(baseTransform.gameObject);
                if (noteSpawner.baseObjectCoroutines.ContainsKey(baseTransform.gameObject))
                {
                    noteSpawner.StopCoroutine(noteSpawner.baseObjectCoroutines[baseTransform.gameObject]);
                    noteSpawner.baseObjectCoroutines.Remove(baseTransform.gameObject);
                }
                if (noteSpawner.baseObjectTimes.ContainsKey(baseTransform.gameObject))
                {
                    noteSpawner.baseObjectTimes.Remove(baseTransform.gameObject);
                }
                Debug.Log($"Removing baseTransform for note {note.Midi} with scale.z={finalBaseScale.z:F4}, elapsedTime={elapsedTime:F4}, totalReductionTime={totalReductionTime:F4}, isSustained={note.IsSustained}");
                Destroy(baseTransform.gameObject);
            }
        }

        noteSpawner.SpawnedObjects.Remove(noteGameObject);
        Destroy(noteGameObject);
    }

    // Выключаем свечение палочки
    if (sustainedNoteMaterials.ContainsKey(note.Midi))
    {
        Material stickMaterial = sustainedNoteMaterials[note.Midi];
        if (stickMaterial != null)
        {
            stickMaterial.SetFloat("_GlowEnabled", 0);
        }
        sustainedNoteMaterials.Remove(note.Midi);
    }

    OnNoteSustainEnd?.Invoke(note.Midi);
    reducingNotes.Remove(note);
}
}