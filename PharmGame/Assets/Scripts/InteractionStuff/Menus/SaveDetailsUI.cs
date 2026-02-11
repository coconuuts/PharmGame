using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems.Persistence;
using System;

namespace Systems.UI
{
    public class SaveDetailsUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image screenshotImage;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI playtimeText;
        [SerializeField] private GameObject contentObject; // Object to show if data exists

        public void Clear()
        {
            if (contentObject) contentObject.SetActive(false);
        }

        public void SetData(GameData data, Texture2D screenshot)
        {
            if (data == null)
            {
                Clear();
                return;
            }

            if (contentObject) contentObject.SetActive(true);

            // Set Text
            if (characterNameText) characterNameText.text = data.CharacterName;
            if (dateText) dateText.text = string.IsNullOrEmpty(data.LastSaveDate) ? "Unknown Date" : data.LastSaveDate;
            
            if (playtimeText)
            {
                TimeSpan t = TimeSpan.FromSeconds(data.TotalPlayTimeSeconds);
                playtimeText.text = $"Playtime: {(int)t.TotalHours:D2}:{t.Minutes:D2}";
            }

            // Set Screenshot
            if (screenshotImage)
            {
                if (screenshot != null)
                {
                    // Create a sprite from the texture
                    Sprite shot = Sprite.Create(screenshot, new Rect(0, 0, screenshot.width, screenshot.height), new Vector2(0.5f, 0.5f));
                    screenshotImage.sprite = shot;
                    screenshotImage.color = Color.white;
                }
                else
                {
                    // Fallback color or default image
                    screenshotImage.sprite = null;
                    screenshotImage.color = Color.black; 
                }
            }
        }
    }
}