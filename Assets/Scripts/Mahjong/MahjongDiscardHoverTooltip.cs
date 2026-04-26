using UnityEngine;
using UnityEngine.EventSystems;

namespace Mahjong
{
	/// <summary>
	/// 쏘인 버림패에 부착되어, 호버 시 WaitInfoPanel을 표시.
	/// 컨트롤러가 트리거 발생 시 Init(...)으로 활성화. 일반 버림패에서는 비활성 상태로 유지.
	/// </summary>
	public class MahjongDiscardHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		MahjongWaitInfoPanel panel;
		string enemyName;
		WaitGroup wait;
		int damageHalfHearts;
		bool ready;

		public void Init(string enemy, WaitGroup w, int dmgHalf, MahjongWaitInfoPanel p)
		{
			enemyName = enemy;
			wait = w;
			damageHalfHearts = dmgHalf;
			panel = p;
			ready = panel != null;
			enabled = true;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (!ready) return;
			panel.Show(transform as RectTransform, enemyName, wait, damageHalfHearts);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (!ready) return;
			panel.Hide();
		}

		void OnDisable()
		{
			if (ready) panel.Hide();
		}
	}
}
