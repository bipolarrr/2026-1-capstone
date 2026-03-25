// Assets/Scripts/Dice/DiceViewportInteraction.cs
// RenderTexture 뷰포트 위에서 마우스 좌표를 DiceCamera 레이로 변환해
// 3D 주사위의 호버 / 클릭 이벤트를 감지한다.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DiceViewportInteraction : MonoBehaviour
{
	[SerializeField] private Camera   diceCamera;
	[SerializeField] private RawImage viewport;
	[SerializeField] private int      diceLayerIndex = 8; // "Dice3D" 레이어 인덱스

	public event System.Action<YachtDie> OnHoverEnter;
	public event System.Action<YachtDie> OnHoverExit;
	public event System.Action<YachtDie> OnClicked;

	private YachtDie currentHovered;

	private void Update()
	{
		var mouse = Mouse.current;
		if (mouse == null) return;

		var die = RaycastDie(mouse.position.ReadValue());

		// 호버 상태 전환
		if (die != currentHovered)
		{
			if (currentHovered != null) OnHoverExit?.Invoke(currentHovered);
			currentHovered = die;
			if (currentHovered != null) OnHoverEnter?.Invoke(currentHovered);
		}

		// 클릭
		if (mouse.leftButton.wasPressedThisFrame && currentHovered != null)
			OnClicked?.Invoke(currentHovered);
	}

	/// <summary>
	/// 마우스 위치 → RawImage UV → DiceCamera 레이 → Physics.Raycast
	/// </summary>
	private YachtDie RaycastDie(Vector2 mousePosition)
	{
		if (diceCamera == null || viewport == null) return null;

		// ScreenSpaceOverlay Canvas 이므로 camera = null
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				viewport.rectTransform, mousePosition, null, out var localPoint))
			return null;

		var rect = viewport.rectTransform.rect;
		float uvX = (localPoint.x - rect.xMin) / rect.width;
		float uvY = (localPoint.y - rect.yMin) / rect.height;

		if (uvX < 0f || uvX > 1f || uvY < 0f || uvY > 1f) return null;

		var ray  = diceCamera.ViewportPointToRay(new Vector3(uvX, uvY, 0f));
		int mask = 1 << diceLayerIndex;

		// RaycastAll 로 벽 등 비-주사위 콜라이더를 관통하여 주사위를 찾음
		var hits = Physics.RaycastAll(ray, 200f, mask);
		YachtDie closest         = null;
		float    closestDistance  = float.MaxValue;
		foreach (var hit in hits)
		{
			var die = hit.collider.GetComponentInParent<YachtDie>();
			if (die != null && hit.distance < closestDistance)
			{
				closest         = die;
				closestDistance  = hit.distance;
			}
		}
		return closest;
	}
}
