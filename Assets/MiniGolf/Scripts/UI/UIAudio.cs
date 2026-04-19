using UnityEngine;

namespace MiniGolf
{
    public class UIAudio : MonoBehaviour
    {
        [SerializeField]
        private AudioSource source;

        [SerializeField]
        private AudioClip enterSFX;

        [SerializeField]
        private AudioClip downSFX;

        void Start()
        {
            HoverButton.OnEnter += () => source.PlayOneShot(enterSFX);
            HoverButton.OnDown += () => source.PlayOneShot(downSFX);
        }
    }
}