/// <summary>
/// 캐릭터 선택 씬에서 인게임 씬으로 선택값을 전달하는 정적 컨텍스트.
/// SceneManager.LoadScene 이후에도 값이 유지된다.
/// </summary>
public static class CharacterSelectionContext
{
	public static CharacterType SelectedCharacter { get; set; } = CharacterType.Mahjong;
}
