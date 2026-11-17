using OctoberStudio.Audio;
using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Input;
using OctoberStudio.Pool;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OctoberStudio.UI
{
    public class StageFailedScreen : MonoBehaviour
    {
        [SerializeField] protected Canvas canvas;
        [SerializeField] protected Image backgroundImage;
        [SerializeField] protected RectTransform titleRect;
        [SerializeField] protected Image titleGlow;

        [Space]
        [SerializeField] protected RectTransform rewardsTitle;
        [SerializeField] protected GridLayoutGroup rewardsGrid;

        [Space]
        [SerializeField] protected Button reviveButton;
        [SerializeField] protected Button continueButton;

        [Tooltip("复活按钮与继续按钮之间的间距")]
        [SerializeField] protected float reviveButtonOffset = 20f;

        [Tooltip("奖励网格底边到上方按钮顶边的距离，保证按钮不会盖住奖励卡片")]
        [SerializeField] protected float buttonTopGap = 120f;

        [Tooltip("按钮距离屏幕底部的最小留白")]
        [SerializeField] protected float screenBottomPadding = 30f;

        [SerializeField] protected SmallRewardCard coinsRewardCard;
        [SerializeField] protected GameObject itemRewardCardPrefab;

        [Space]
        [SerializeField] protected AudioData failSound;

        protected PoolComponent<SmallItemRewardCard> cardsPool;

        protected RectTransform gridRect;

        /// <summary>
        /// 奖励区（Rewards 节点）在布局前的初始位置，每次重新布局前都要复位
        /// </summary>
        protected Vector2 rewardsGroupBasePosition;

        protected float glowAlpha;

        protected ContinuePlayingSave ContinuePlayingSave { get; set; }

        public bool IsOpen { get; protected set; } = false;

        protected bool isInitialized = false;

        /// <summary>
        /// 本局是否已经用广告复活过（只能复活一次）
        /// </summary>
        protected bool isAdReviveUsed = false;

        /// <summary>
        /// 正在等待广告结果，防止重复点击
        /// </summary>
        protected bool isWaitingForAd = false;

        protected virtual void Init()
        {
            ContinuePlayingSave = GameController.SaveManager.GetSave<ContinuePlayingSave>("Continue Playing");

            gridRect = rewardsGrid.GetComponent<RectTransform>();

            cardsPool = new PoolComponent<SmallItemRewardCard>(itemRewardCardPrefab, 5, gridRect);

            var rewardsGroup = gridRect.parent as RectTransform;

            if (rewardsGroup != null)
            {
                rewardsGroupBasePosition = rewardsGroup.anchoredPosition;
            }

            glowAlpha = titleGlow.color.a;

            continueButton.onClick.AddListener(OnContinueButtonClicked);

            if (reviveButton != null)
            {
                reviveButton.onClick.AddListener(OnReviveButtonClicked);
            }

            isInitialized = true;
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);

            if (!isInitialized)
            {
                Init();
            }

            GameController.AudioManager.PlayAudio(failSound);

            var pickedUpItems = StageController.DropManager.PickedUpItems;
            pickedUpItems.Sort((item1, item2) =>
            {
                var level1 = GameController.ArmoryManager.GetItemLevel(item1.ItemId);
                var level2 = GameController.ArmoryManager.GetItemLevel(item2.ItemId);

                return level2.ItemRarity.CompareTo(level1.ItemRarity);
            });

            EasingManager.DoTimeScale(0, 0.3f);

            canvas.enabled = true;
            continueButton.enabled = false;
            SetReviveButtonEnabled(false);

            // 广告复活后还会再次失败，这块屏幕会被第二次打开，先隐藏上一轮遗留下来的奖励卡。
            // 注意金币卡也是 Grid 的子节点，不能一起关掉，否则奖励里少了金币、网格高度也会算错
            for (int i = 0; i < gridRect.childCount; i++)
            {
                var child = gridRect.GetChild(i);

                if (coinsRewardCard != null && child == coinsRewardCard.transform) continue;

                child.gameObject.SetActive(false);
            }

            if (coinsRewardCard != null)
            {
                coinsRewardCard.gameObject.SetActive(true);
            }

            backgroundImage.SetAlpha(0);
            backgroundImage.DoAlpha(1, 0.3f).SetUnscaledTime(true);

            titleRect.localScale = new Vector3(0, 0.9f, 1f);
            titleRect.DoLocalScale(Vector3.one, 0.3f).SetDelay(0.1f).SetUnscaledTime(true).SetEasing(EasingType.SineOut);

            titleGlow.SetAlpha(0);
            titleGlow.DoAlpha(glowAlpha, 0.3f).SetDelay(0.3f).SetUnscaledTime(true).SetEasing(EasingType.SineOut);

            rewardsTitle.localScale = new Vector3(0, 0.9f, 1f);
            rewardsTitle.DoLocalScale(Vector3.one, 0.3f).SetDelay(0.6f).SetUnscaledTime(true).SetEasing(EasingType.SineOut);

            coinsRewardCard.SetAmount(GameController.TempGold);
            coinsRewardCard.Show(0.7f);

            for (int i = 0; i < pickedUpItems.Count; i++)
            {
                var item = GameController.ArmoryManager.GetItemData(pickedUpItems[i].ItemId);

                var card = cardsPool.GetEntity();
                card.SetItem(item);
                card.SetAmount(1);

                card.Show(0.7f + i * 0.05f);
            }

            var buttonRect = GetButtonRect(continueButton);
            var reviveRect = isAdReviveUsed ? null : GetButtonRect(reviveButton);

            EasingManager.DoNextFrame(() =>
            {
                gridRect.SetSizeDeltaY(rewardsGrid.preferredHeight);
                LayoutButtons();
            });

            buttonRect.localScale = new Vector3(0, 0.9f, 1f);
            buttonRect.DoLocalScale(Vector3.one, 0.3f)
                .SetDelay(0.7f + pickedUpItems.Count * 0.05f + 0.5f)
                .SetUnscaledTime(true)
                .SetEasing(EasingType.SineOut)
                .SetOnFinish(() => continueButton.enabled = true);

            // 广告复活按钮：一局只出现一次，出现在继续按钮上方
            if (reviveRect != null && !isAdReviveUsed)
            {
                reviveRect.localScale = new Vector3(0, 0.9f, 1f);
                reviveRect.DoLocalScale(Vector3.one, 0.3f)
                    .SetDelay(0.7f + pickedUpItems.Count * 0.05f + 0.35f)
                    .SetUnscaledTime(true)
                    .SetEasing(EasingType.SineOut)
                    .SetOnFinish(() => SetReviveButtonEnabled(true));
            }

            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            GameController.InputManager.onInputChanged += OnInputChanged;

            // 注意：这里故意不调 ContinuePlayingSave.Disable()。
            // 它会把 RevivedTimes 一起清零，一旦玩家用广告复活，RevivedTimes 归零后
            // Second Chance 的复活次数就"恢复"了，下次死亡会白送一次复活；
            // 同时 ActiveRoomId / XP 也会被清空。Disable() 改到真正放弃这一局时再调。
            IsOpen = true;
        }

        /// <summary>
        /// 以奖励网格的真实底边为基准摆放两个按钮，保证按钮永远不会盖住奖励卡片，
        /// 同时在奖励很多时把按钮压在屏幕内（必要时整体上移奖励区）。
        /// </summary>
        protected virtual void LayoutButtons()
        {
            var buttonRect = GetButtonRect(continueButton);
            var reviveRect = isAdReviveUsed ? null : GetButtonRect(reviveButton);

            if (buttonRect == null) return;

            var rewardsGroup = gridRect.parent as RectTransform;

            if (rewardsGroup != null)
            {
                // 每次重新布局前先把奖励区复位，避免上一次的上移累积
                rewardsGroup.anchoredPosition = rewardsGroupBasePosition;
            }

            bool showRevive = reviveRect != null;

            float buttonHeight = buttonRect.sizeDelta.y;
            float halfHeight = buttonHeight * 0.5f;

            float minGap = Mathf.Min(buttonTopGap, 40f);
            float gap = Mathf.Max(buttonTopGap, minGap);

            float gridBottom = GetGridBottomY();

            float reviveY = gridBottom - gap - halfHeight;
            float continueY = showRevive ? reviveY - (buttonHeight + reviveButtonOffset) : reviveY;

            // 按钮被挤出屏幕时，先压缩奖励与按钮之间的留白，再整体上移奖励区
            float lowestAllowed = -GetHalfScreenHeight() + screenBottomPadding + halfHeight;
            float overflow = lowestAllowed - continueY;

            if (overflow > 0f)
            {
                float compress = Mathf.Min(overflow, gap - minGap);

                if (compress > 0f)
                {
                    gap -= compress;
                    overflow -= compress;

                    gridBottom = GetGridBottomY();

                    reviveY = gridBottom - gap - halfHeight;
                    continueY = showRevive ? reviveY - (buttonHeight + reviveButtonOffset) : reviveY;
                }
            }

            if (overflow > 0f && rewardsGroup != null)
            {
                float shift = Mathf.Min(overflow, 100f);

                rewardsGroup.anchoredPosition = rewardsGroupBasePosition + new Vector2(0f, shift);

                gridBottom = GetGridBottomY();

                reviveY = gridBottom - gap - halfHeight;
                continueY = showRevive ? reviveY - (buttonHeight + reviveButtonOffset) : reviveY;
            }

            // 最后一道保险：无论空间多紧，按钮都不许到奖励网格底边以上
            float highestAllowedY = gridBottom - minGap - halfHeight;

            reviveY = Mathf.Min(reviveY, highestAllowedY);
            continueY = showRevive ? Mathf.Min(continueY, reviveY - (buttonHeight + reviveButtonOffset)) : reviveY;

            if (showRevive)
            {
                reviveRect.SetAnchoredPositionY(reviveY);
            }

            buttonRect.SetAnchoredPositionY(continueY);
        }

        /// <summary>
        /// 奖励网格底边在父级（整屏容器）坐标系里的 Y
        /// </summary>
        protected virtual float GetGridBottomY()
        {
            float groupY = 0f;

            if (gridRect.parent is RectTransform group)
            {
                groupY = group.anchoredPosition.y;
            }

            return groupY + gridRect.anchoredPosition.y - gridRect.sizeDelta.y;
        }

        protected virtual float GetHalfScreenHeight()
        {
            if (canvas != null && canvas.transform is RectTransform canvasRect && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.height * 0.5f;
            }

            if (transform is RectTransform selfRect && selfRect.rect.height > 0f)
            {
                return selfRect.rect.height * 0.5f;
            }

            return 960f;
        }

        protected virtual RectTransform GetButtonRect(Button button)
        {
            if (button == null) return null;

            if (button.image != null) return button.image.rectTransform;

            return button.transform as RectTransform;
        }

        protected virtual void OnInputChanged(InputType prevInput, InputType newInput)
        {
            if (newInput == InputType.Gamepad)
            {
                EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            }
        }

        protected virtual void Clear()
        {
            GameController.InputManager.onInputChanged -= OnInputChanged;
        }

        /// <summary>
        /// 复活按钮的显示与可点状态
        /// </summary>
        protected virtual void SetReviveButtonEnabled(bool interactable)
        {
            if (reviveButton == null) return;

            // 广告复活只能用一次，用掉之后按钮不再出现
            reviveButton.gameObject.SetActive(!isAdReviveUsed);

            reviveButton.enabled = interactable && !isAdReviveUsed && !isWaitingForAd;
        }

        protected virtual void OnReviveButtonClicked()
        {
            if (!IsOpen || isAdReviveUsed || isWaitingForAd) return;

            GameController.AudioManager.PlayButtonClick();

            isWaitingForAd = true;
            SetReviveButtonEnabled(false);

#if UNITY_EDITOR
            // 编辑器里 TT 接口是 mock，直接复活方便测试；真机上才真正播插屏广告
            OnAdReviveCompleted();
#else
            var adManager = AdManager.Instance;

            if (adManager == null)
            {
                Debug.LogError("[广告] 场景里没有 AdManager，无法播放插屏广告");
                OnAdReviveFailed("广告服务未初始化");
                return;
            }

            adManager.ShowInterstitialAd(OnAdReviveCompleted, OnAdReviveFailed);
#endif
        }

        /// <summary>
        /// 插屏广告已经展示并关闭，复活生效
        /// </summary>
        protected virtual void OnAdReviveCompleted()
        {
            isWaitingForAd = false;
            isAdReviveUsed = true;

            Debug.Log("[广告] 插屏广告播放完成，广告复活生效");

            Clear();
            SetReviveButtonEnabled(false);

            canvas.enabled = false;
            gameObject.SetActive(false);

            IsOpen = false;

            Time.timeScale = 1f;

            // 这批掉落在上面已经结算发放过了，清空避免第二次死亡时重复发放
            StageController.DropManager.ClearGrantedItems();

            StageController.Player.ReviveFromAd();
        }

        /// <summary>
        /// 广告没能展示（未加载完成、冷却中、平台异常等），本次不复活
        /// </summary>
        protected virtual void OnAdReviveFailed(string message)
        {
            Debug.LogWarning($"[广告] 插屏广告没能展示，本次复活未生效：{message}");

            isWaitingForAd = false;

            // 复活机会不消耗，按钮恢复可点，等广告加载好可以再试
            SetReviveButtonEnabled(true);

            var adErrorPrefab = Resources.Load<GameObject>("AdError");

            if (adErrorPrefab != null)
            {
                var adErrorInstance = Instantiate(adErrorPrefab, transform.position, Quaternion.identity);
                Destroy(adErrorInstance, 1.5f);
            }
        }

        protected virtual void OnContinueButtonClicked()
        {
            GameController.AudioManager.PlayButtonClick();

            Clear();

            // 这里是真正放弃这一局：清掉未完成的关卡进度（含 RevivedTimes）
            ContinuePlayingSave.Disable();

            Time.timeScale = 1f;
            StageController.ReturnToMainMenu();
        }
    }
}
