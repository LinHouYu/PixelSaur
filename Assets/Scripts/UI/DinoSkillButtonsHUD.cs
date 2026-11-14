using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 王者荣耀弧形布局的双技能虚拟按钮：
    /// 位于右下角攻击按钮左上方弧线位置，独立触发技能（替代原攻击键双击触发）。
    /// </summary>
    public class DinoSkillButtonsHUD : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject canvasObj = GameObject.Find("HUD_Canvas");
            if (canvasObj == null) return;
            if (canvasObj.GetComponent<DinoSkillButtonsHUD>() == null)
            {
                canvasObj.AddComponent<DinoSkillButtonsHUD>();
            }
        }

        private void Awake()
        {
            CreateSkillButton(
                "DinoSkill1Button",
                new Vector2(-365f, 365f),
                new Vector2(136f, 136f),
                Resources.Load<Sprite>("UI/icon_dino_skill1"),
                OnSkill1Clicked
            );

            CreateSkillButton(
                "DinoSkill2Button",
                new Vector2(-530f, 310f),
                new Vector2(136f, 136f),
                Resources.Load<Sprite>("UI/icon_dino_skill2"),
                OnSkill2Clicked
            );
        }

        private void CreateSkillButton(string objName, Vector2 anchoredPos, Vector2 size, Sprite iconSprite, System.Action onClick)
        {
            if (transform.Find(objName) != null) return;

            GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(SkillButtonPressHandler));
            btnObj.transform.SetParent(transform, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            img.sprite = iconSprite;
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (iconSprite == null)
            {
                img.color = new Color(0.95f, 0.45f, 0.15f, 0.9f);
            }

            SkillButtonPressHandler handler = btnObj.GetComponent<SkillButtonPressHandler>();
            handler.Initialize(img, onClick);
        }

        private static void OnSkill1Clicked()
        {
            if (DinoCombatController.Instance != null)
            {
                DinoCombatController.Instance.TryChargeDashSkill();
            }
        }

        private static void OnSkill2Clicked()
        {
            if (DinoCombatController.Instance != null)
            {
                DinoCombatController.Instance.TryChargeDashSkill();
            }
        }

        private class SkillButtonPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            private Image _image;
            private System.Action _onClick;

            public void Initialize(Image image, System.Action onClick)
            {
                _image = image;
                _onClick = onClick;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                transform.localScale = Vector3.one * 0.92f;
                if (_image != null) _image.color = new Color(0.76f, 0.76f, 0.76f, 1f);
                _onClick?.Invoke();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                transform.localScale = Vector3.one;
                if (_image != null) _image.color = Color.white;
            }
        }
    }
}
