using UnityEngine;

[System.Serializable]
public class EnemyInfo
{
	public string name;
	public int hp;
	public int maxHp;
	public int attack;
	public Color color;
	public Sprite sprite;

	public EnemyInfo(string name, int maxHp, int attack, Color color, Sprite sprite = null)
	{
		this.name = name;
		this.maxHp = maxHp;
		this.hp = maxHp;
		this.attack = attack;
		this.color = color;
		this.sprite = sprite;
	}

	public EnemyInfo Clone()
	{
		var copy = new EnemyInfo(name, maxHp, attack, color, sprite);
		copy.hp = hp;
		return copy;
	}

	public bool IsAlive => hp > 0;

	public void TakeDamage(int damage)
	{
		hp = Mathf.Max(0, hp - damage);
	}
}
