using UnityEngine;

namespace MiniGolf
{
    // 공용 오디오 헬퍼.
    // AudioSource.PlayClipAtPoint 는 기본 3D 라 카메라 거리로 볼륨이 크게 감쇠됨.
    // SFX 가 거리 무관하게 일정하게 들려야 할 때 (코인/폭탄/홀인 등) PlaySfx2D 사용.
    public static class AudioUtil
    {
        // 임시 AudioSource 를 생성해 2D (spatialBlend=0) 로 clip 재생. 재생 끝나면 자동 파괴.
        public static void PlaySfx2D(AudioClip clip, float volume = 1f)
        {
            if(clip == null) return;
            var go = new GameObject("OneShotSFX_" + clip.name);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.spatialBlend = 0f;         // 2D — 거리 감쇠 없음
            src.bypassReverbZones = true;
            src.playOnAwake = false;
            src.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }
    }
}
