using UnityEngine;

/// <summary>
/// 씬 빌더가 편집 시점에 한 스테이지의 모든 스프라이트를 로드해 담는 컨테이너.
/// 런타임 컨트롤러는 [SerializeField] StageSpriteBundle[]로 받아서
/// 현재 활성 스테이지 ID에 해당하는 번들을 찾아 사용한다.
/// mobSprites의 인덱스는 StageData.mobPool 순서와 1:1 대응.
/// </summary>
[System.Serializable]
public class StageSpriteBundle
{
	public string   stageId;
	public Sprite   background;
	public Sprite[] mobSprites;
	public EnemySpriteAnimationSet[] mobAnimations;
	public Sprite   bossSprite;
}

[System.Serializable]
public class EnemySpriteAnimationSet
{
	public Sprite[] idleSprites;
	public Sprite[] attackSprites;
	public Sprite[] hitSprites;
}
