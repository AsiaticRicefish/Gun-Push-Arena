using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundId
{
    LobbyBgm,
    GameBgm,

    UiConfirm,
    MapGenerateSuccess,
    MapGenerateFail,
    GameStart,

    Jump,
    Fire,
    Hit,
    Fall,
    Respawn,

    Win,
    Lose
}

/// <summary>
/// Inspector에서 설정하는 사운드 1개의 정보입니다.
/// 클립, 기본 볼륨, 반복 재생 여부를 함께 관리합니다.
/// </summary>
[Serializable]
public class SoundEntry
{
    public SoundId Id;
    public AudioClip Clip;

    [Range(0f, 1f)]
    public float Volume = 1f;

    public bool Loop;
}

/// <summary>
/// 프로젝트에서 사용하는 사운드 참조를 한 곳에서 관리하는 ScriptableObject입니다.
/// Inspector에서는 List로 편하게 편집하고, 런타임에서는 Dictionary 캐시로 빠르게 조회합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "AudioData",
    menuName = "Gun Push Arena/Audio/Audio Data")]
public class AudioData : ScriptableObject
{
    [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

    private readonly Dictionary<SoundId, SoundEntry> soundCache = new Dictionary<SoundId, SoundEntry>();

    public bool TryGetSound(SoundId id, out SoundEntry sound)
    {
        // 도메인 리로드나 에셋 로드 순서에 따라 캐시가 비어 있을 수 있으므로 조회 직전에 보장합니다.
        EnsureCache();
        return soundCache.TryGetValue(id, out sound);
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    private void EnsureCache()
    {
        if (soundCache.Count == 0)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        soundCache.Clear();

        foreach (SoundEntry sound in sounds)
        {
            if (sound == null)
            {
                continue;
            }

            // 같은 SoundId가 여러 번 등록되면 Inspector 목록에서 더 아래에 있는 항목을 우선합니다.
            soundCache[sound.Id] = sound;
        }
    }
}