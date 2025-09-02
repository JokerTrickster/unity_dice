using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매칭 진행 애니메이터
/// 매칭 상태에 따른 시각적 애니메이션과 진행 상황을 표시합니다.
/// 60FPS 유지를 위해 최적화된 애니메이션을 제공합니다.
/// </summary>
public class MatchingProgressAnimator : MonoBehaviour
{
    #region UI References
    [Header("Progress Elements")]
    [SerializeField] private Image[] progressDots;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressFill;
    [SerializeField] private Text progressText;
    
    [Header("Loading Animation")]
    [SerializeField] private Image loadingSpinner;
    [SerializeField] private Image[] pulsatingElements;
    [SerializeField] private ParticleSystem matchingParticles;
    
    [Header("Progress Indicators")]
    [SerializeField] private GameObject searchingIndicator;
    [SerializeField] private GameObject foundIndicator;
    [SerializeField] private GameObject failedIndicator;
    
    [Header("Animation Settings")]
    [SerializeField] private float dotAnimationSpeed = 0.5f;
    [SerializeField] private float spinnerSpeed = 180f; // degrees per second
    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField] private float progressBarSpeed = 2f;
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Colors")]
    [SerializeField] private Color activeDotColor = Color.white;
    [SerializeField] private Color inactiveDotColor = Color.gray;
    [SerializeField] private Color progressFillColor = Color.green;
    [SerializeField] private Color pulseColor = Color.yellow;
    
    [Header("Effects")]
    [SerializeField] private AudioClip progressSound;
    [SerializeField] private AudioClip completionSound;
    [SerializeField] private bool enableParticleEffects = true;
    [SerializeField] private bool enableSoundEffects = true;
    #endregion
    
    #region Private Fields
    private MatchingState _currentState = MatchingState.Idle;
    private float _currentProgress = 0f;
    private float _targetProgress = 0f;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private TimeSpan _estimatedTime = TimeSpan.Zero;
    
    // Animation coroutines
    private Coroutine _dotAnimationCoroutine;
    private Coroutine _spinnerAnimationCoroutine;
    private Coroutine _pulseAnimationCoroutine;
    private Coroutine _progressBarCoroutine;
    
    // Cached components for performance
    private AudioSource _audioSource;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    
    // Animation state tracking
    private bool _isAnimating = false;
    private int _currentDotIndex = 0;
    private float _spinnerRotation = 0f;
    private bool _isInitialized = false;
    
    // Performance optimization
    private readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    private readonly WaitForFixedUpdate _waitForFixedUpdate = new WaitForFixedUpdate();
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        ValidateReferences();
        SetupInitialState();
    }
    
    private void Start()
    {
        Initialize();
    }
    
    private void OnEnable()
    {
        if (_isInitialized)
        {
            RestartAnimations();
        }
    }
    
    private void OnDisable()
    {
        StopAllAnimations();
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
    #endregion
    
    #region Initialization
    private void InitializeComponents()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        _rectTransform = GetComponent<RectTransform>();
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    private void ValidateReferences()
    {
        if (progressDots == null || progressDots.Length == 0)
            Debug.LogWarning("[MatchingProgressAnimator] Progress dots array is empty");
            
        if (progressBar == null)
            Debug.LogWarning("[MatchingProgressAnimator] Progress bar is missing");
            
        if (loadingSpinner == null)
            Debug.LogWarning("[MatchingProgressAnimator] Loading spinner is missing");
    }
    
    private void SetupInitialState()
    {
        // Initialize progress dots
        if (progressDots != null)
        {
            foreach (var dot in progressDots)
            {
                if (dot != null)
                    dot.color = inactiveDotColor;
            }
        }
        
        // Initialize progress bar
        if (progressBar != null)
        {
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }
        
        // Initialize indicators
        SetIndicatorVisibility(searchingIndicator, false);
        SetIndicatorVisibility(foundIndicator, false);
        SetIndicatorVisibility(failedIndicator, false);
        
        // Initialize particles
        if (matchingParticles != null)
        {
            matchingParticles.Stop();
        }
    }
    
    public void Initialize()
    {
        if (_isInitialized) return;
        
        Debug.Log("[MatchingProgressAnimator] Initializing component");
        
        SetState(MatchingState.Idle);
        RefreshUI();
        _isInitialized = true;
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 애니메이션 상태를 설정합니다
    /// </summary>
    public void SetState(MatchingState newState)
    {
        if (_currentState == newState) return;
        
        var previousState = _currentState;
        _currentState = newState;
        
        Debug.Log($"[MatchingProgressAnimator] State changed: {previousState} -> {newState}");
        
        UpdateAnimationState();
        UpdateIndicators();
        
        if (newState == MatchingState.Found)
        {
            PlayCompletionEffects();
        }
    }
    
    /// <summary>
    /// 진행률을 업데이트합니다
    /// </summary>
    public void UpdateProgress(float progress, TimeSpan elapsed, TimeSpan estimated)
    {
        _targetProgress = Mathf.Clamp01(progress);
        _elapsedTime = elapsed;
        _estimatedTime = estimated;
        
        if (_progressBarCoroutine == null && progressBar != null)
        {
            _progressBarCoroutine = StartCoroutine(SmoothProgressUpdate());
        }
        
        UpdateProgressText();
    }
    
    /// <summary>
    /// UI를 새로고침합니다
    /// </summary>
    public void RefreshUI()
    {
        UpdateIndicators();
        UpdateProgressText();
        
        if (_currentState == MatchingState.Searching && !_isAnimating)
        {
            StartSearchingAnimations();
        }
    }
    
    /// <summary>
    /// 애니메이션 속도를 설정합니다
    /// </summary>
    public void SetAnimationSpeed(float speedMultiplier)
    {
        dotAnimationSpeed = Mathf.Max(0.1f, dotAnimationSpeed * speedMultiplier);
        spinnerSpeed = Mathf.Max(10f, spinnerSpeed * speedMultiplier);
        pulseSpeed = Mathf.Max(0.1f, pulseSpeed * speedMultiplier);
    }
    
    /// <summary>
    /// 파티클 효과를 활성화/비활성화합니다
    /// </summary>
    public void SetParticleEffects(bool enabled)
    {
        enableParticleEffects = enabled;
        
        if (!enabled && matchingParticles != null)
        {
            matchingParticles.Stop();
        }
        else if (enabled && _currentState == MatchingState.Searching && matchingParticles != null)
        {
            matchingParticles.Play();
        }
    }
    #endregion
    
    #region Private Animation Methods
    private void UpdateAnimationState()
    {
        StopAllAnimations();
        
        switch (_currentState)
        {
            case MatchingState.Idle:
                SetupIdleState();
                break;
                
            case MatchingState.Searching:
                StartSearchingAnimations();
                break;
                
            case MatchingState.Found:
                StartFoundAnimations();
                break;
                
            case MatchingState.Starting:
                StartStartingAnimations();
                break;
                
            case MatchingState.Failed:
                StartFailedAnimations();
                break;
                
            case MatchingState.Cancelled:
                StartCancelledAnimations();
                break;
        }
    }
    
    private void SetupIdleState()
    {
        ResetAllElements();
        _isAnimating = false;
    }
    
    private void StartSearchingAnimations()
    {
        if (_isAnimating) return;
        
        _isAnimating = true;
        
        // Start dot animation
        if (progressDots != null && progressDots.Length > 0)
        {
            _dotAnimationCoroutine = StartCoroutine(AnimateProgressDots());
        }
        
        // Start spinner animation
        if (loadingSpinner != null)
        {
            _spinnerAnimationCoroutine = StartCoroutine(AnimateSpinner());
        }
        
        // Start pulse animation
        if (pulsatingElements != null && pulsatingElements.Length > 0)
        {
            _pulseAnimationCoroutine = StartCoroutine(AnimatePulsatingElements());
        }
        
        // Start particle effects
        if (enableParticleEffects && matchingParticles != null)
        {
            matchingParticles.Play();
        }
        
        // Show progress bar
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 0f;
        }
    }
    
    private void StartFoundAnimations()
    {
        _isAnimating = false;
        
        // Flash all dots green
        if (progressDots != null)
        {
            foreach (var dot in progressDots)
            {
                if (dot != null)
                    dot.color = progressFillColor;
            }
        }
        
        // Complete progress bar
        if (progressBar != null)
        {
            progressBar.value = 1f;
        }
    }
    
    private void StartStartingAnimations()
    {
        // Similar to found but with different visual cues
        StartFoundAnimations();
    }
    
    private void StartFailedAnimations()
    {
        _isAnimating = false;
        
        // Flash red and reset
        StartCoroutine(FlashFailedState());
    }
    
    private void StartCancelledAnimations()
    {
        _isAnimating = false;
        ResetAllElements();
    }
    
    private IEnumerator AnimateProgressDots()
    {
        while (_isAnimating && _currentState == MatchingState.Searching)
        {
            if (progressDots != null && progressDots.Length > 0)
            {
                // Reset all dots
                foreach (var dot in progressDots)
                {
                    if (dot != null)
                        dot.color = inactiveDotColor;
                }
                
                // Animate current dot
                if (progressDots[_currentDotIndex] != null)
                {
                    progressDots[_currentDotIndex].color = activeDotColor;
                }
                
                _currentDotIndex = (_currentDotIndex + 1) % progressDots.Length;
            }
            
            yield return new WaitForSecondsRealtime(dotAnimationSpeed);
        }
    }
    
    private IEnumerator AnimateSpinner()
    {
        while (_isAnimating && _currentState == MatchingState.Searching)
        {
            if (loadingSpinner != null)
            {
                _spinnerRotation += spinnerSpeed * Time.unscaledDeltaTime;
                loadingSpinner.transform.rotation = Quaternion.Euler(0, 0, -_spinnerRotation);
            }
            
            yield return _waitForEndOfFrame;
        }
    }
    
    private IEnumerator AnimatePulsatingElements()
    {
        float time = 0f;
        
        while (_isAnimating && _currentState == MatchingState.Searching)
        {
            time += Time.unscaledDeltaTime * pulseSpeed;
            float alpha = (Mathf.Sin(time) + 1f) * 0.5f;
            
            if (pulsatingElements != null)
            {
                foreach (var element in pulsatingElements)
                {
                    if (element != null)
                    {
                        var color = element.color;
                        color.a = alpha;
                        element.color = color;
                    }
                }
            }
            
            yield return _waitForEndOfFrame;
        }
    }
    
    private IEnumerator SmoothProgressUpdate()
    {
        while (Mathf.Abs(_currentProgress - _targetProgress) > 0.01f)
        {
            _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, 
                Time.unscaledDeltaTime * progressBarSpeed);
            
            if (progressBar != null)
            {
                progressBar.value = progressCurve.Evaluate(_currentProgress);
            }
            
            if (progressFill != null)
            {
                progressFill.fillAmount = _currentProgress;
            }
            
            yield return _waitForEndOfFrame;
        }
        
        _currentProgress = _targetProgress;
        if (progressBar != null)
            progressBar.value = progressCurve.Evaluate(_currentProgress);
            
        _progressBarCoroutine = null;
    }
    
    private IEnumerator FlashFailedState()
    {
        Color originalColor = Color.white;
        
        // Flash red
        if (progressDots != null)
        {
            foreach (var dot in progressDots)
            {
                if (dot != null)
                {
                    originalColor = dot.color;
                    dot.color = Color.red;
                }
            }
        }
        
        yield return new WaitForSecondsRealtime(0.5f);
        
        // Reset to original
        if (progressDots != null)
        {
            foreach (var dot in progressDots)
            {
                if (dot != null)
                    dot.color = originalColor;
            }
        }
        
        yield return new WaitForSecondsRealtime(1f);
        
        // Reset to idle
        ResetAllElements();
    }
    #endregion
    
    #region UI Update Methods
    private void UpdateIndicators()
    {
        SetIndicatorVisibility(searchingIndicator, _currentState == MatchingState.Searching);
        SetIndicatorVisibility(foundIndicator, _currentState == MatchingState.Found || _currentState == MatchingState.Starting);
        SetIndicatorVisibility(failedIndicator, _currentState == MatchingState.Failed);
    }
    
    private void SetIndicatorVisibility(GameObject indicator, bool visible)
    {
        if (indicator != null)
            indicator.SetActive(visible);
    }
    
    private void UpdateProgressText()
    {
        if (progressText == null) return;
        
        string text = "";
        
        if (_currentState == MatchingState.Searching)
        {
            if (_estimatedTime > TimeSpan.Zero)
            {
                float percentage = (float)(_elapsedTime.TotalSeconds / _estimatedTime.TotalSeconds) * 100f;
                text = $"진행률: {Mathf.Min(percentage, 100f):F0}%";
            }
            else
            {
                text = $"대기 시간: {_elapsedTime:mm\\:ss}";
            }
        }
        else if (_currentState == MatchingState.Found)
        {
            text = "매칭 완료!";
        }
        else if (_currentState == MatchingState.Failed)
        {
            text = "매칭 실패";
        }
        
        progressText.text = text;
    }
    
    private void ResetAllElements()
    {
        // Reset dots
        if (progressDots != null)
        {
            foreach (var dot in progressDots)
            {
                if (dot != null)
                    dot.color = inactiveDotColor;
            }
        }
        
        // Reset progress bar
        if (progressBar != null)
        {
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }
        
        // Reset spinner
        if (loadingSpinner != null)
        {
            loadingSpinner.transform.rotation = Quaternion.identity;
        }
        
        // Reset pulse elements
        if (pulsatingElements != null)
        {
            foreach (var element in pulsatingElements)
            {
                if (element != null)
                {
                    var color = element.color;
                    color.a = 1f;
                    element.color = color;
                }
            }
        }
        
        // Stop particles
        if (matchingParticles != null)
        {
            matchingParticles.Stop();
        }
        
        _currentProgress = 0f;
        _targetProgress = 0f;
        _currentDotIndex = 0;
        _spinnerRotation = 0f;
    }
    
    private void RestartAnimations()
    {
        if (_currentState == MatchingState.Searching)
        {
            StartSearchingAnimations();
        }
    }
    
    private void StopAllAnimations()
    {
        _isAnimating = false;
        
        if (_dotAnimationCoroutine != null)
        {
            StopCoroutine(_dotAnimationCoroutine);
            _dotAnimationCoroutine = null;
        }
        
        if (_spinnerAnimationCoroutine != null)
        {
            StopCoroutine(_spinnerAnimationCoroutine);
            _spinnerAnimationCoroutine = null;
        }
        
        if (_pulseAnimationCoroutine != null)
        {
            StopCoroutine(_pulseAnimationCoroutine);
            _pulseAnimationCoroutine = null;
        }
        
        if (_progressBarCoroutine != null)
        {
            StopCoroutine(_progressBarCoroutine);
            _progressBarCoroutine = null;
        }
    }
    
    private void PlayCompletionEffects()
    {
        if (enableSoundEffects && _audioSource != null && completionSound != null)
        {
            _audioSource.PlayOneShot(completionSound);
        }
        
        if (enableParticleEffects && matchingParticles != null)
        {
            // Brief celebration particle burst
            matchingParticles.Stop();
            matchingParticles.Play();
        }
    }
    #endregion
    
    #region Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Test Animation States")]
    private void TestAnimationStates()
    {
        if (!Application.isPlaying) return;
        
        StartCoroutine(TestAnimationSequence());
    }
    
    private IEnumerator TestAnimationSequence()
    {
        var states = new[] 
        { 
            MatchingState.Idle, 
            MatchingState.Searching, 
            MatchingState.Found, 
            MatchingState.Starting,
            MatchingState.Failed,
            MatchingState.Cancelled
        };
        
        foreach (var state in states)
        {
            Debug.Log($"Testing animation state: {state}");
            SetState(state);
            
            if (state == MatchingState.Searching)
            {
                // Simulate progress updates
                for (float progress = 0f; progress <= 1f; progress += 0.2f)
                {
                    UpdateProgress(progress, TimeSpan.FromSeconds(progress * 30), TimeSpan.FromSeconds(30));
                    yield return new WaitForSeconds(0.5f);
                }
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }
        
        SetState(MatchingState.Idle);
    }
    #endif
    #endregion
}