using UnityEngine;

/// <summary>
/// BGM과 SFX 재생을 담당하는 전역 오디오 관리자입니다.
/// AudioData는 클립 참조를 관리하고, 이 클래스는 재생, 정지, 볼륨, on/off 상태를 관리합니다.
/// </summary>
public class AudioManager : GlobalSingleton<AudioManager>
{
    [Header("Data")]
    [SerializeField] private AudioData audioData;

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Settings")]
    [SerializeField] private bool bgmEnabled = true;
    [SerializeField] private bool sfxEnabled = true;

    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private float currentBgmEntryVolume = 1f;

    /// <summary>
    /// BGM 재생 여부입니다. 끄면 현재 BGM을 일시정지하고, 다시 켜면 이어서 재생합니다.
    /// </summary>
    public bool BgmEnabled
    {
        get => bgmEnabled;
        set
        {
            bgmEnabled = value;
            ApplyBgmSettings();
        }
    }

    /// <summary>
    /// SFX 재생 여부입니다. 끄면 이후 PlaySfx 호출을 무시합니다.
    /// </summary>
    public bool SfxEnabled
    {
        get => sfxEnabled;
        set => sfxEnabled = value;
    }

    /// <summary>
    /// 전체 BGM 볼륨입니다. 현재 BGM의 SoundEntry 볼륨과 곱해져 최종 볼륨이 됩니다.
    /// </summary>
    public float BgmVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            ApplyBgmSettings();
        }
    }

    /// <summary>
    /// 전체 SFX 볼륨입니다. 각 SoundEntry 볼륨과 곱해져 최종 볼륨이 됩니다.
    /// </summary>
    public float SfxVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        // Inspector에서 AudioSource를 연결하지 않아도 실행 가능한 상태로 보정합니다.
        EnsureAudioSources();
        ApplyBgmSettings();
    }

    /// <summary>
    /// 지정한 사운드를 BGM으로 반복 재생합니다.
    /// 같은 클립이 이미 재생 중이면 처음부터 다시 시작하지 않습니다.
    /// </summary>
    public void PlayBgm(SoundId soundId)
    {
        if (!bgmEnabled || audioData == null || bgmSource == null)
        {
            return;
        }

        if (!audioData.TryGetSound(soundId, out SoundEntry sound))
        {
            return;
        }

        if (sound == null || sound.Clip == null)
        {
            return;
        }

        if (bgmSource.clip == sound.Clip && bgmSource.isPlaying)
        {
            return;
        }

        currentBgmEntryVolume = sound.Volume;

        bgmSource.clip = sound.Clip;
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume * currentBgmEntryVolume;
        bgmSource.Play();
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 정지하고 클립 참조를 비웁니다.
    /// </summary>
    public void StopBgm()
    {
        if (bgmSource == null)
        {
            return;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
        currentBgmEntryVolume = 1f;
    }

    /// <summary>
    /// 지정한 사운드를 SFX로 한 번 재생합니다.
    /// AudioData에 항목이 없거나 클립이 비어 있으면 조용히 무시합니다.
    /// </summary>
    public void PlaySfx(SoundId soundId)
    {
        if (!sfxEnabled || audioData == null || sfxSource == null)
        {
            return;
        }

        if (!audioData.TryGetSound(soundId, out SoundEntry sound))
        {
            return;
        }

        if (sound == null || sound.Clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(sound.Clip, sfxVolume * sound.Volume);
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    private void ApplyBgmSettings()
    {
        if (bgmSource == null)
        {
            return;
        }

        // 전체 BGM 볼륨과 현재 BGM 항목의 개별 볼륨을 함께 반영합니다.
        bgmSource.volume = bgmVolume * currentBgmEntryVolume;

        if (!bgmEnabled)
        {
            bgmSource.Pause();
            return;
        }

        if (bgmSource.clip != null && !bgmSource.isPlaying)
        {
            bgmSource.UnPause();
        }
    }
}