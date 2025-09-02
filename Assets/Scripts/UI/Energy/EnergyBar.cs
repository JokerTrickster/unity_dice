using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에너지 진행바 컴포넌트
/// 에너지 상태를 시각적으로 표현하며, 부드러운 애니메이션과 색상 변화를 제공합니다.
/// </summary>
public class EnergyBar : MonoBehaviour
{
    #region UI References
    [Header("Progress Bar Components")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image backgroundImage;
    
    [Header("Visual Effects")]
    [SerializeField] private Image glowEffect;
    [SerializeField] private ParticleSystem fullEnergyParticles;
    [SerializeField] private GameObject pulseEffect;
    
    [Header("Color Configuration")]
    [SerializeField] private Gradient energyColorGradient;
    [SerializeField] private Color lowEnergyColor = Color.red;
    [SerializeField] private Color mediumEnergyColor = Color.yellow;
    [SerializeField] private Color fullEnergyColor = Color.green;
    [SerializeField] private Color backgroundDefaultColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.8f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float glowAnimationSpeed = 2.0f;
    [SerializeField] private bool enableSmoothAnimation = true;
    #endregion

    #region Private Fields
    private Coroutine _animationCoroutine;
    private Coroutine _glowCoroutine;
    private Coroutine _pulseCoroutine;
    
    // Current state
    private float _currentValue = 0f;
    private float _targetValue = 0f;
    private int _lastEnergyAmount = -1;
    private bool _isAnimating = false;
    
    // Visual state
    private bool _isLowEnergy = false;
    private bool _isFullEnergy = false;
    private bool _showGlowEffect = false;
    
    // Animation cache
    private Vector3 _originalScale;
    private Color _originalGlowColor;
    #endregion

    #region Events
    public static event Action<float> OnEnergyBarUpdated;
    public static event Action OnAnimationCompleted;
    public static event Action OnDepletionEffectTriggered;
    public static event Action OnFullEnergyEffectTriggered;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        CacheOriginalValues();
        SetupColorGradient();
    }

    private void Start()
    {
        InitializeBar();
    }

    private void OnDestroy()
    {
        StopAllAnimations();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Validate required components
        if (progressSlider == null)
        {
            progressSlider = GetComponent<Slider>();
            if (progressSlider == null)
            {
                Debug.LogError("[EnergyBar] Slider component is missing!");
                return;
            }
        }

        if (fillImage == null)
        {
            // Try to find fill image in slider
            if (progressSlider.fillRect != null)
            {
                fillImage = progressSlider.fillRect.GetComponent<Image>();
            }
        }

        if (backgroundImage == null && progressSlider.GetComponent<Image>() != null)
        {
            backgroundImage = progressSlider.GetComponent<Image>();
        }

        // Setup slider properties
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.value = 0f;
        progressSlider.interactable = false; // Display only
    }

    private void CacheOriginalValues()
    {
        _originalScale = transform.localScale;
        
        if (glowEffect != null)
        {
            _originalGlowColor = glowEffect.color;
        }
    }

    private void SetupColorGradient()
    {
        if (energyColorGradient == null)
        {
            // Create default gradient
            energyColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            
            colorKeys[0] = new GradientColorKey(lowEnergyColor, 0.0f);
            colorKeys[1] = new GradientColorKey(mediumEnergyColor, 0.5f);
            colorKeys[2] = new GradientColorKey(fullEnergyColor, 1.0f);
            
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            
            energyColorGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    private void InitializeBar()
    {
        // Set initial colors
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundDefaultColor;
        }

        if (fillImage != null)
        {
            fillImage.color = energyColorGradient.Evaluate(0f);
        }

        // Hide effects initially
        SetGlowEffect(false);
        
        if (pulseEffect != null)
        {
            pulseEffect.SetActive(false);
        }

        if (fullEnergyParticles != null && fullEnergyParticles.isPlaying)
        {
            fullEnergyParticles.Stop();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 에너지 바 업데이트 (즉시 또는 애니메이션)
    /// </summary>
    public void UpdateEnergyBar(int currentEnergy, int maxEnergy, float percentage)
    {
        if (maxEnergy <= 0) return;

        float newValue = Mathf.Clamp01(percentage);
        
        // Skip if same value and not first time
        if (Mathf.Approximately(newValue, _targetValue) && _lastEnergyAmount == currentEnergy)
            return;

        _targetValue = newValue;
        _lastEnergyAmount = currentEnergy;

        if (enableSmoothAnimation && Application.isPlaying)
        {
            AnimateToValue(newValue);
        }
        else
        {
            SetValueImmediate(newValue);
        }

        UpdateVisualEffects(newValue, currentEnergy);
        
        OnEnergyBarUpdated?.Invoke(newValue);
    }

    /// <summary>
    /// 특정 값으로 애니메이션
    /// </summary>
    public void AnimateToValue(float targetValue)
    {
        targetValue = Mathf.Clamp01(targetValue);
        _targetValue = targetValue;

        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
        }

        _animationCoroutine = StartCoroutine(AnimateBarCoroutine(targetValue));
    }

    /// <summary>
    /// 즉시 값 설정 (애니메이션 없음)
    /// </summary>
    public void SetValueImmediate(float value)
    {
        value = Mathf.Clamp01(value);
        _currentValue = value;
        _targetValue = value;
        
        if (progressSlider != null)
        {
            progressSlider.value = value;
        }

        UpdateFillColor(value);
    }

    /// <summary>
    /// 고갈 효과 트리거
    /// </summary>
    public void TriggerDepletionEffect()
    {
        StartCoroutine(DepletionEffectCoroutine());
        OnDepletionEffectTriggered?.Invoke();
    }

    /// <summary>
    /// 가득찬 에너지 효과 트리거
    /// </summary>
    public void TriggerFullEnergyEffect()
    {
        StartCoroutine(FullEnergyEffectCoroutine());
        OnFullEnergyEffectTriggered?.Invoke();
    }

    /// <summary>
    /// 펄스 효과 시작/중지
    /// </summary>
    public void SetPulseEffect(bool enable)
    {
        if (pulseEffect != null)
        {
            pulseEffect.SetActive(enable);
            
            if (enable && _pulseCoroutine == null)
            {
                _pulseCoroutine = StartCoroutine(PulseEffectCoroutine());
            }
            else if (!enable && _pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }
    }

    /// <summary>
    /// 글로우 효과 시작/중지
    /// </summary>
    public void SetGlowEffect(bool enable)
    {
        _showGlowEffect = enable;
        
        if (glowEffect != null)
        {
            glowEffect.gameObject.SetActive(enable);
            
            if (enable && _glowCoroutine == null)
            {
                _glowCoroutine = StartCoroutine(GlowEffectCoroutine());
            }
            else if (!enable && _glowCoroutine != null)
            {
                StopCoroutine(_glowCoroutine);
                _glowCoroutine = null;
            }
        }
    }
    #endregion

    #region Animation Coroutines
    private IEnumerator AnimateBarCoroutine(float targetValue)
    {
        _isAnimating = true;
        float startValue = _currentValue;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            
            // Apply animation curve
            float curveValue = animationCurve.Evaluate(t);
            
            _currentValue = Mathf.Lerp(startValue, targetValue, curveValue);
            
            if (progressSlider != null)
            {
                progressSlider.value = _currentValue;
            }

            UpdateFillColor(_currentValue);
            
            yield return null;
        }

        // Ensure final value
        _currentValue = targetValue;
        if (progressSlider != null)
        {
            progressSlider.value = targetValue;
        }
        UpdateFillColor(targetValue);

        _isAnimating = false;
        _animationCoroutine = null;
        
        OnAnimationCompleted?.Invoke();
    }

    private IEnumerator DepletionEffectCoroutine()
    {
        // Flash effect for energy depletion
        if (fillImage != null)
        {
            Color originalColor = fillImage.color;
            Color flashColor = Color.red;
            
            // Flash red quickly
            for (int i = 0; i < 3; i++)
            {
                fillImage.color = flashColor;
                yield return new WaitForSeconds(0.1f);
                fillImage.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Shake effect
        yield return StartCoroutine(ShakeEffectCoroutine(0.5f, 5f));
    }

    private IEnumerator FullEnergyEffectCoroutine()
    {
        // Glow and particle effects
        SetGlowEffect(true);
        
        if (fullEnergyParticles != null)
        {
            fullEnergyParticles.Play();
        }

        // Scale up briefly
        Vector3 targetScale = _originalScale * 1.1f;
        yield return StartCoroutine(ScaleEffectCoroutine(targetScale, 0.3f));
        yield return StartCoroutine(ScaleEffectCoroutine(_originalScale, 0.2f));

        // Keep glow for a bit longer
        yield return new WaitForSeconds(2f);
        
        if (!_isFullEnergy) // Only turn off if no longer full
        {
            SetGlowEffect(false);
        }
        
        if (fullEnergyParticles != null)
        {
            fullEnergyParticles.Stop();
        }
    }

    private IEnumerator GlowEffectCoroutine()
    {
        if (glowEffect == null) yield break;

        while (_showGlowEffect)
        {
            float alpha = (Mathf.Sin(Time.time * glowAnimationSpeed) + 1f) * 0.5f;
            Color glowColor = _originalGlowColor;
            glowColor.a = alpha * 0.8f; // Max alpha of 0.8
            
            glowEffect.color = glowColor;
            
            yield return null;
        }

        // Reset glow
        glowEffect.color = _originalGlowColor;
        _glowCoroutine = null;
    }

    private IEnumerator PulseEffectCoroutine()
    {
        if (pulseEffect == null) yield break;

        while (pulseEffect.activeInHierarchy)
        {
            // Simple pulse scaling
            float scale = 1f + (Mathf.Sin(Time.time * 3f) * 0.1f);
            pulseEffect.transform.localScale = Vector3.one * scale;
            
            yield return null;
        }

        _pulseCoroutine = null;
    }

    private IEnumerator ShakeEffectCoroutine(float duration, float intensity)
    {
        Vector3 originalPosition = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            Vector3 shakeOffset = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                0f
            ) * intensity * (1f - elapsed / duration); // Decay shake

            transform.localPosition = originalPosition + shakeOffset;
            
            yield return null;
        }

        // Reset position
        transform.localPosition = originalPosition;
    }

    private IEnumerator ScaleEffectCoroutine(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            yield return null;
        }

        transform.localScale = targetScale;
    }
    #endregion

    #region Visual Updates
    private void UpdateFillColor(float value)
    {
        if (fillImage != null)
        {
            Color newColor = energyColorGradient.Evaluate(value);
            fillImage.color = newColor;
        }
    }

    private void UpdateVisualEffects(float value, int currentEnergy)
    {
        bool isLow = value <= 0.2f;
        bool isFull = value >= 1.0f;
        bool isEmpty = currentEnergy <= 0;

        // Update low energy state
        if (isLow != _isLowEnergy)
        {
            _isLowEnergy = isLow;
            if (isLow && !isEmpty)
            {
                SetPulseEffect(true); // Pulse when low but not empty
            }
            else
            {
                SetPulseEffect(false);
            }
        }

        // Update full energy state
        if (isFull != _isFullEnergy)
        {
            _isFullEnergy = isFull;
            SetGlowEffect(isFull);
        }

        // Handle empty state
        if (isEmpty && currentEnergy != _lastEnergyAmount)
        {
            SetPulseEffect(false); // Stop pulse when empty
        }
    }
    #endregion

    #region Utility Methods
    private void StopAllAnimations()
    {
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }

        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
    }

    /// <summary>
    /// 현재 진행률 반환
    /// </summary>
    public float GetCurrentValue()
    {
        return _currentValue;
    }

    /// <summary>
    /// 애니메이션 진행 중인지 확인
    /// </summary>
    public bool IsAnimating()
    {
        return _isAnimating;
    }
    #endregion

    #region Editor Methods
    #if UNITY_EDITOR
    [ContextMenu("Test Low Energy")]
    private void TestLowEnergy()
    {
        UpdateEnergyBar(20, 100, 0.2f);
        TriggerDepletionEffect();
    }

    [ContextMenu("Test Full Energy")]
    private void TestFullEnergy()
    {
        UpdateEnergyBar(100, 100, 1.0f);
        TriggerFullEnergyEffect();
    }

    [ContextMenu("Test Animation")]
    private void TestAnimation()
    {
        AnimateToValue(0.75f);
    }
    #endif
    #endregion
}