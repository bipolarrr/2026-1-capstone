using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DiceViewportInteraction : MonoBehaviour
{
	[SerializeField] private Camera   diceCamera;
	[SerializeField] private RawImage viewport;
	[SerializeField] private Camera   vaultCamera;
	[SerializeField] private RawImage vaultViewport;
	[SerializeField] private int      diceLayerIndex = 8; // "Dice3D" 레이어 인덱스

	public event System.Action<YachtDie> OnHoverEnter;
	public event System.Action<YachtDie> OnHoverExit;
	public event System.Action<YachtDie> OnClicked;

	private YachtDie currentHovered;
	private readonly RaycastHit[] hitBuffer = new RaycastHit[10];

	private void Update()
	{
		var mouse = Mouse.current;
		if (mouse == null) return;

		var mousePos = mouse.position.ReadValue();
		var die = RaycastDieFromViewport(diceCamera, viewport, mousePos)
			   ?? RaycastDieFromViewport(vaultCamera, vaultViewport, mousePos);

		if (die != currentHovered)
		{
			if (currentHovered != null) OnHoverExit?.Invoke(currentHovered);
			currentHovered = die;
			if (currentHovered != null) OnHoverEnter?.Invoke(currentHovered);
		}

		if (mouse.leftButton.wasPressedThisFrame && currentHovered != null)
			OnClicked?.Invoke(currentHovered);
	}

	private YachtDie RaycastDieFromViewport(Camera cam, RawImage vp, Vector2 mousePosition)
	{
		if (cam == null || vp == null) return null;

		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				vp.rectTransform, mousePosition, null, out var localPoint))
			return null;

		var rect = vp.rectTransform.rect;
		float uvX = (localPoint.x - rect.xMin) / rect.width;
		float uvY = (localPoint.y - rect.yMin) / rect.height;

		if (uvX < 0f || uvX > 1f || uvY < 0f || uvY > 1f) return null;

		var ray  = cam.ViewportPointToRay(new Vector3(uvX, uvY, 0f));
		int mask = 1 << diceLayerIndex;

		int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, 200f, mask);
		YachtDie closest         = null;
		float    closestDistance  = float.MaxValue;
		for (int i = 0; i < hitCount; i++)
		{
			var die = hitBuffer[i].collider.GetComponentInParent<YachtDie>();
			if (die != null && hitBuffer[i].distance < closestDistance)
			{
				closest         = die;
				closestDistance  = hitBuffer[i].distance;
			}
		}
		return closest;
	}
}
