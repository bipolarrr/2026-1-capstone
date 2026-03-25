using UnityEngine;

/// <summary>
/// 캐릭터 선택 씬에서 사용하는 캐릭터 1명의 데이터.
/// Inspector에서 직접 입력하거나, SceneBuilder에서 기본값으로 초기화된다.
/// </summary>
[System.Serializable]
public class CharacterData
{
	[Header("식별")]
	public CharacterType characterType;
	public string displayName;

	[Header("설명 텍스트")]
	[TextArea(2, 4)] public string conceptDescription;
	[TextArea(2, 4)] public string attackDescription;

	[Header("미리보기")]
	public RuntimeAnimatorController previewAnimatorController;
	public Sprite previewSprite;
	public Color previewFallbackColor = new Color(0.25f, 0.38f, 0.75f, 1f);

	[Header("잠금 설정")]
	public bool isAvailable = true;
	public string unavailableMessage = "아직 개발되지 않음";
}
