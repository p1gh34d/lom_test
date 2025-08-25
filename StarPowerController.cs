using UnityEngine;

public class StarPowerController : MonoBehaviour
{
    [Header("Star Power Settings")]
    [Range(0, 1)] public float starPower = 0f;
    public Color starTint = new Color(0.55f, 0.75f, 1f, 0.35f);
    [Range(-180, 180)] public float starHueShift = 120f;
    [Range(0, 2)] public float starIntensity = 1.0f;
    
    [Header("Animation Settings")]
    public float activationSpeed = 2f;
    public float deactivationSpeed = 3f;
    public bool useSmoothTransition = true;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    private bool isStarPowerActive = false;
    private float targetStarPower = 0f;
    
    // Singleton pattern for easy access
    public static StarPowerController Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Initialize shader properties
        UpdateShaderProperties();
    }
    
    private void Update()
    {
        // Smooth transition if enabled
        if (useSmoothTransition && Mathf.Abs(starPower - targetStarPower) > 0.01f)
        {
            float speed = isStarPowerActive ? activationSpeed : deactivationSpeed;
            starPower = Mathf.MoveTowards(starPower, targetStarPower, speed * Time.deltaTime);
            UpdateShaderProperties();
        }
    }
    
    /// <summary>
    /// Activate Star Power
    /// </summary>
    public void ActivateStarPower()
    {
        if (!isStarPowerActive)
        {
            isStarPowerActive = true;
            targetStarPower = 1f;
            
            if (!useSmoothTransition)
            {
                starPower = 1f;
                UpdateShaderProperties();
            }
            
            if (showDebugInfo)
                Debug.Log("Star Power Activated!");
        }
    }
    
    /// <summary>
    /// Deactivate Star Power
    /// </summary>
    public void DeactivateStarPower()
    {
        if (isStarPowerActive)
        {
            isStarPowerActive = false;
            targetStarPower = 0f;
            
            if (!useSmoothTransition)
            {
                starPower = 0f;
                UpdateShaderProperties();
            }
            
            if (showDebugInfo)
                Debug.Log("Star Power Deactivated!");
        }
    }
    
    /// <summary>
    /// Toggle Star Power
    /// </summary>
    public void ToggleStarPower()
    {
        if (isStarPowerActive)
            DeactivateStarPower();
        else
            ActivateStarPower();
    }
    
    /// <summary>
    /// Set Star Power intensity directly (0-1)
    /// </summary>
    public void SetStarPower(float intensity)
    {
        starPower = Mathf.Clamp01(intensity);
        targetStarPower = starPower;
        isStarPowerActive = starPower > 0.01f;
        UpdateShaderProperties();
    }
    
    /// <summary>
    /// Update all shader properties globally
    /// </summary>
    private void UpdateShaderProperties()
    {
        // Set global shader properties that all our custom shaders will use
        Shader.SetGlobalFloat("_StarPower", starPower);
        Shader.SetGlobalColor("_StarTint", starTint);
        Shader.SetGlobalFloat("_StarHueShift", starHueShift);
        Shader.SetGlobalFloat("_StarIntensity", starIntensity);
    }
    
    /// <summary>
    /// Update shader properties for a specific material
    /// </summary>
    public void UpdateMaterialProperties(Material material)
    {
        if (material != null)
        {
            material.SetFloat("_StarPower", starPower);
            material.SetColor("_StarTint", starTint);
            material.SetFloat("_StarHueShift", starHueShift);
            material.SetFloat("_StarIntensity", starIntensity);
        }
    }
    
    /// <summary>
    /// Update shader properties for a specific renderer
    /// </summary>
    public void UpdateRendererProperties(Renderer renderer)
    {
        if (renderer != null && renderer.material != null)
        {
            UpdateMaterialProperties(renderer.material);
        }
    }
    
    /// <summary>
    /// Update shader properties for all renderers in children
    /// </summary>
    public void UpdateAllChildrenProperties(Transform parent)
    {
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            UpdateRendererProperties(renderer);
        }
    }
    
    // Inspector validation
    private void OnValidate()
    {
        // Clamp values
        starPower = Mathf.Clamp01(starPower);
        starIntensity = Mathf.Clamp(starIntensity, 0f, 2f);
        starHueShift = Mathf.Clamp(starHueShift, -180f, 180f);
        
        // Update shaders in editor
        if (Application.isPlaying)
        {
            UpdateShaderProperties();
        }
    }
    
    // Debug info
    private void OnGUI()
    {
        if (showDebugInfo && Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("Star Power Controller Debug");
            GUILayout.Label($"Star Power: {starPower:F2}");
            GUILayout.Label($"Target: {targetStarPower:F2}");
            GUILayout.Label($"Active: {isStarPowerActive}");
            GUILayout.Label($"Hue Shift: {starHueShift:F0}°");
            GUILayout.Label($"Intensity: {starIntensity:F2}");
            
            if (GUILayout.Button("Activate"))
                ActivateStarPower();
            if (GUILayout.Button("Deactivate"))
                DeactivateStarPower();
            if (GUILayout.Button("Toggle"))
                ToggleStarPower();
            
            GUILayout.EndArea();
        }
    }
}