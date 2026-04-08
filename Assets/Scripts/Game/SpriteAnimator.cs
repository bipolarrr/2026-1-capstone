using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피벗 하단 기준으로 Y축 스케일을 살짝 늘였다 줄였다 하는 idle 호흡 애니메이션.
/// 피벗이 (0.5, 0) 이므로 발은 고정되고 위쪽만 늘어난다.
/// </summary>
public class SpriteAnimator : MonoBehaviour
{
	[SerializeField] float amplitude = 0.03f;   // 스케일 변화량 (±3 %)
	[SerializeField] float speed = 2f;          // 호흡 속도

	Vector3 baseScale;

	void Start()
	{
		baseScale = transform.localScale;
	}

	void Update()
	{
		float t = Mathf.Sin(Time.time * speed) * amplitude;
		transform.localScale = new Vector3(
			baseScale.x,
			baseScale.y * (1f + t),
			baseScale.z);
	}
}
