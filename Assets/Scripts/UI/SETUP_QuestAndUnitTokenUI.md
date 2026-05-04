# 프로시저럴 퀘스트 선택지 / 유닛 업그레이드 토큰 UI 연결 안내

## QuestChoiceCellController (퀘스트 선택 셀 프리팹)

스크립트가 붙은 GameObject에서 인스펙터로 다음을 연결합니다.

| 필드 | 설명 |
|------|------|
| **Resource Icon Image** | 납품 대상 자원 아이콘 (기존과 동일) |
| **Required Amount Text** | 요구 수량만 표시 (자원 숫자) |
| **Token Reward Root** | (선택) 토큰 보상 줄 전체를 묶는 부모 오브젝트. 연결 시 `SetActive`로 토큰이 없을 때 숨김 처리에 사용합니다. 비워 두면 루트는 쓰지 않고 아래 이미지·텍스트만 제어합니다. |
| **Token Icon Image** | 토큰 보상 전용 아이콘 `Image` |
| **Token Reward Text** | `+N` 형식으로 표시할 `TMP_Text` |
| **Quest Token Icon Sprite** | 위 토큰 이미지에 쓸 `Sprite` (프로젝트 내 아이콘 에셋 지정) |
| **Accept Button** | 수락 버튼 (기존과 동일) |

**프리팹 구성 예:** 자원 행(아이콘 + 요구량) 아래에 자식으로 `TokenRow`를 두고, 그 안에 토큰 `Image`와 `TextMeshPro - Text (UI)`를 배치한 뒤 `Token Reward Root`에 `TokenRow`를, 각각 `Token Icon Image` / `Token Reward Text`에 연결합니다.

---

## UnitUpgradeCell (유닛 업그레이드 행 프리팹)

기존에 `resourceContent` / `resourceInfoCellPrefab`으로 연결해 두었던 참조는 Unity가 `tokenCostContainer` / `tokenInfoCellPrefab`으로 자동 이전합니다(`FormerlySerializedAs`). 새로 프리팹을 만들 때는 아래 이름으로 맞추면 됩니다.

| 필드 | 설명 |
|------|------|
| **Token Cost Container** | 토큰 비용용 `ResourceInfoCell` 인스턴스가 붙는 부모 `Transform` (예: Horizontal Layout Group이 있는 빈 오브젝트) |
| **Token Info Cell Prefab** | 기존과 동일하게 `ResourceInfoCell`이 붙은 UI 프리팹 |
| **Gameplay Token Icon** | `ResourceInfoCell`의 아이콘 영역에 표시할 토큰 `Sprite`. 비워 두면 아이콘은 숨기고 비용 숫자만 표시합니다. |

나머지 필드(제목, 설명, 레벨, 업그레이드 버튼 등)는 기존과 동일합니다.

---

## ProceduralQuestPanelController (퀘스트 패널)

| 필드 | 설명 |
|------|------|
| **Active Token Amount Text** | 진행 중 퀘스트의 **완료 시 지급 토큰 보상** 합계를 `+N` 형식으로 표시합니다. 토큰 보상이 없으면 비웁니다. 진행 UI(`Active Quest UI` 섹션)에 `TMP_Text`를 두고 연결합니다. |

---

## ProceduralQuestManager (씬 또는 프리팹)

| 필드 | 설명 |
|------|------|
| **Token Reward Amount Range** | 토큰 보상량의 **정수 범위** (min, max). 런타임에는 이 범위 안에서 **10의 배수**로만 랜덤되며, 결과는 최소 **10**으로 보정됩니다. 다양한 값을 내려면 예: `(10, 50)`처럼 10단위가 들어갈 여지가 있는 구간을 쓰는 것이 좋습니다. |

---

## 관련 스크립트 (코드만으로 동작하는 부분)

- **GameplayTokenWallet**: `ProceduralQuestManager` / `UnitUpgradeProgress` / `UnitUpgradeUIController`에서 `EnsureExists`로 생성·탐색됩니다. 별도 프리팹에 붙일 필요는 없습니다.
- **ResourceInfoCell.SetTokenCost**: 유닛 업그레이드 셀에서는 토큰 아이콘 + **필요 토큰 수만** (예: `99`) 표시, 잔액 부족 시 빨간색으로 갱신합니다. `보유/필요` 형식이 필요하면 네 번째 인자 `labelShowsBalanceFraction`을 `true`로 호출하면 됩니다.
