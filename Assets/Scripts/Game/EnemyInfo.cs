using UnityEngine;

[System.Serializable]
public class EnemyInfo
{
	public string name;
	public int hp;
	public int maxHp;
	public int rank;
	public Color color;
	public Sprite sprite;

	/// <summary>적의 마지막 주사위 결과 (반격 페이즈에서 설정).</summary>
	[System.NonSerialized]
	public EnemyDiceResult lastDiceResult;

	public EnemyInfo(string name, int maxHp, int rank, Color color, Sprite sprite = null)
	{
		this.name = name;
		this.maxHp = maxHp;
		this.hp = maxHp;
		this.rank = rank;
		this.color = color;
		this.sprite = sprite;
	}

	public EnemyInfo Clone()
	{
		var copy = new EnemyInfo(name, maxHp, rank, color, sprite);
		copy.hp = hp;
		return copy;
	}

	public bool IsAlive => hp > 0;

	public void TakeDamage(int damage)
	{
		hp = Mathf.Max(0, hp - damage);
	}

	/// <summary>랭크를 별 문자열로 변환.</summary>
	public string RankStars => new string('★', rank);
}
