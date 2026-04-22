using UnityEngine;

namespace MiniGolf
{
    // OnCollisionEnter 시 clips 배열에서 무작위로 하나를 골라 2D 로 재생.
    // MODAK PUTT 글자처럼 여러 오브젝트가 부딪히면서 bass 가 다양한 stone hit 을 섞어 내도록 설계.
    // 쿨다운 + 최소 충돌 속도 가드로 한 프레임에 수십번 터지거나 작은 떨림에 소리 나는 것 방지.
    public class ImpactSounds : MonoBehaviour
    {
        [Tooltip("무작위로 선택될 impact 사운드 목록. 여러 개 넣을수록 덜 반복적으로 들림.")]
        [SerializeField] private AudioClip[] clips;

        [Tooltip("재생 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.6f;

        [Tooltip("이 속도 미만의 충돌은 무시 (m/s). 가벼운 떨림에 소리 나는 것 방지.")]
        [SerializeField] private float minImpactSpeed = 1.0f;

        [Tooltip("연속 재생 방지용 쿨다운 (초). 빠른 연쇄 충돌에서 소리 쌓임 완화.")]
        [SerializeField] private float cooldown = 0.08f;

        [Tooltip("충돌 속도가 클수록 볼륨이 커지도록 스케일. 0 이면 비활성 (volume 고정).")]
        [SerializeField] private float velocityToVolumeScale = 0f;
        [Tooltip("velocityToVolumeScale 가 적용될 때 볼륨 최댓값")]
        [Range(0f, 1f)]
        [SerializeField] private float maxVolume = 1f;

        private float lastPlayTime = -10f;

        void OnCollisionEnter(Collision col)
        {
            if(clips == null || clips.Length == 0) return;

            float speed = col.relativeVelocity.magnitude;
            if(speed < minImpactSpeed) return;
            if(Time.time - lastPlayTime < cooldown) return;
            lastPlayTime = Time.time;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if(clip == null) return;

            float v = volume;
            if(velocityToVolumeScale > 0f)
                v = Mathf.Min(maxVolume, volume + speed * velocityToVolumeScale);

            AudioUtil.PlaySfx2D(clip, v);
        }
    }
}
