using UnityEngine;

/// <summary>
/// 컷씬 슬라이드 1장의 데이터.
/// 빌더에서 배열로 생성해 컨트롤러에 주입한다.
/// 슬라이드 개수에 제한 없음 — 배열 크기만 늘리면 된다.
/// </summary>
[System.Serializable]
public class CutsceneSlide
{
	/// <summary>하단 자막 텍스트</summary>
	public string subtitleText;

	/// <summary>상단 영역 GameObject (이미지 또는 무기선택 UI)</summary>
	public GameObject topContent;

	/// <summary>true이면 클릭으로 다음 진행 불가, 무기 선택 대기</summary>
	public bool isWeaponSelect;
}
