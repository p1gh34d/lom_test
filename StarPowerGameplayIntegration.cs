using UnityEngine;
using UnityEngine.UI;

public class StarPowerGameplayIntegration : MonoBehaviour
{
    [Header("UI References")]
    public Image starPowerBar;
    public Image starPowerIcon;
    public Text starPowerText;
    
    [Header("Gameplay Settings")]
    public float starPowerDuration = 10f;
    public float starPowerBuildUpRate = 0.1f;
    public float starPowerDecayRate = 0.05f;
    
    [Header("Visual Effects")]
    public ParticleSystem starPowerParticles;
    public AudioSource starPowerSound;
    public Light starPowerLight;
    
    private float currentStarPower = 0f;
    private bool isStarPowerActive = false;
    private float starPowerTimer = 0f;
    
    private void Start()
    {
        // Initialize UI
        if (starPowerBar != null)
            starPowerBar.fillAmount = 0f;
        
        if (starPowerIcon != null)
            starPowerIcon.gameObject.SetActive(false);
            
        if (starPowerText != null)
            starPowerText.text = "Star Power: 0%";
    }
    
    private void Update()
    {
        UpdateStarPower();
        UpdateUI();
        HandleInput();
    }
    
    private void UpdateStarPower()
    {
        if (isStarPowerActive)
        {
            starPowerTimer -= Time.deltaTime;
            
            if (starPowerTimer <= 0f)
            {
                DeactivateStarPower();
            }
        }
        else
        {
            // Build up star power gradually
            currentStarPower += starPowerBuildUpRate * Time.deltaTime;
            currentStarPower = Mathf.Clamp01(currentStarPower);
        }
    }
    
    private void UpdateUI()
    {
        if (starPowerBar != null)
            starPowerBar.fillAmount = currentStarPower;
            
        if (starPowerText != null)
            starPowerText.text = $"Star Power: {Mathf.RoundToInt(currentStarPower * 100)}%";
            
        if (starPowerIcon != null)
            starPowerIcon.gameObject.SetActive(isStarPowerActive);
    }
    
    private void HandleInput()
    {
        // Example: Activate Star Power with Space key when bar is full
        if (Input.GetKeyDown(KeyCode.Space) && currentStarPower >= 1f && !isStarPowerActive)
        {
            ActivateStarPower();
        }
        
        // Example: Quick Star Power activation with Q key (for testing)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (!isStarPowerActive)
                ActivateStarPower();
            else
                DeactivateStarPower();
        }
    }
    
    public void ActivateStarPower()
    {
        if (currentStarPower >= 1f && !isStarPowerActive)
        {
            isStarPowerActive = true;
            starPowerTimer = starPowerDuration;
            currentStarPower = 0f; // Reset bar
            
            // Activate Star Power effect
            if (StarPowerController.Instance != null)
            {
                StarPowerController.Instance.ActivateStarPower();
            }
            
            // Visual effects
            if (starPowerParticles != null)
                starPowerParticles.Play();
                
            if (starPowerSound != null)
                starPowerSound.Play();
                
            if (starPowerLight != null)
                starPowerLight.enabled = true;
                
            Debug.Log("Star Power Activated! Duration: " + starPowerDuration + " seconds");
        }
    }
    
    public void DeactivateStarPower()
    {
        if (isStarPowerActive)
        {
            isStarPowerActive = false;
            starPowerTimer = 0f;
            
            // Deactivate Star Power effect
            if (StarPowerController.Instance != null)
            {
                StarPowerController.Instance.DeactivateStarPower();
            }
            
            // Stop visual effects
            if (starPowerParticles != null)
                starPowerParticles.Stop();
                
            if (starPowerLight != null)
                starPowerLight.enabled = false;
                
            Debug.Log("Star Power Deactivated!");
        }
    }
    
    // Call this when player hits notes to build up Star Power
    public void AddStarPower(float amount)
    {
        if (!isStarPowerActive)
        {
            currentStarPower += amount;
            currentStarPower = Mathf.Clamp01(currentStarPower);
        }
    }
    
    // Call this when player misses notes to reduce Star Power
    public void ReduceStarPower(float amount)
    {
        if (!isStarPowerActive)
        {
            currentStarPower -= amount;
            currentStarPower = Mathf.Clamp01(currentStarPower);
        }
    }
    
    // Get current Star Power status
    public bool IsStarPowerActive()
    {
        return isStarPowerActive;
    }
    
    public float GetStarPowerPercentage()
    {
        return currentStarPower;
    }
    
    public float GetRemainingTime()
    {
        return starPowerTimer;
    }
}