using UnityEngine;
using UnityEngine.UI;

namespace OctoberStudio.UI
{
    /// <summary>
    /// 奖励广告按钮：点击后弹出激励视频确认框，观看完成后发放金币。
    /// 注意：AdConfirm 弹窗上显示的金币数值需要与这里的 rewardAmount 保持一致。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewardAdButton : MonoBehaviour
    {
        [SerializeField, Tooltip("看完激励视频后发放的金币数量")]
        protected int rewardAmount = 500;

        [SerializeField, Tooltip("激励视频确认弹窗（AdConfirm）所在的物体")]
        protected GameObject popup;

        protected Button button;

        protected virtual void Awake()
        {
            button = GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        protected virtual void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        public virtual void OnClick()
        {
            // 弹窗在场景里默认是关闭的，先激活一次让 AdConfirm.Awake 注册静态实例，
            // 否则 AdConfirm.Show 会从 Resources 里再实例化一个弹窗。
            if (popup != null && !popup.activeSelf)
            {
                popup.SetActive(true);
            }

            AdConfirm.Show(GrantReward);
        }

        protected virtual void GrantReward()
        {
            if (!GameController.IsInitialized || GameController.Gold == null)
            {
                Debug.LogError("[奖励广告] 游戏数据还没初始化，无法发放金币");
                return;
            }

            GameController.Gold.Deposit(rewardAmount);
            GameController.SaveManager?.Save(false);

            Debug.Log($"[奖励广告] 激励视频观看完成，发放 {rewardAmount} 金币");
        }
    }
}
