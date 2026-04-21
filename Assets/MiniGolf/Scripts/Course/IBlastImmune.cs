namespace MiniGolf
{
    // 폭발 AddExplosionForce 영향에서 제외해야 하는 인터랙티브 오브젝트용 마커.
    // 공, 바나나처럼 "공과 상호작용하는" 오브젝트는 이 인터페이스를 구현해 물리 폭발에 쓸려가지 않게 함.
    // 인터페이스 구현만 하면 됨 (메서드 없음).
    public interface IBlastImmune { }
}
