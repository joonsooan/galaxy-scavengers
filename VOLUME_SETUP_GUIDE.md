# 볼륨 조절 시스템 인스펙터 설정 가이드

## 1. FMODVolumeController 설정

### 위치
- GameScene의 씬 루트 또는 별도의 GameObject에 `FMODVolumeController` 컴포넌트 추가
- DontDestroyOnLoad이므로 씬 전환 시에도 유지됨

### 인스펙터 설정 항목

#### VCA Paths
- **Master VCAPath**: 마스터 볼륨 VCA 경로
  - 기본값: `"vca:/Master"`
  - FMOD Studio에서 설정한 실제 VCA 경로로 변경 필요
  
- **Music VCAPath**: 음악 볼륨 VCA 경로
  - 기본값: `"vca:/Music"`
  - FMOD Studio에서 설정한 실제 VCA 경로로 변경 필요
  
- **SFX VCAPath**: 효과음 볼륨 VCA 경로
  - 기본값: `"vca:/SFX"`
  - FMOD Studio에서 설정한 실제 VCA 경로로 변경 필요

#### Default Volumes
- **Default Master Volume**: 마스터 볼륨 기본값 (0.0 ~ 1.0)
- **Default Music Volume**: 음악 볼륨 기본값 (0.0 ~ 1.0)
- **Default SFX Volume**: 효과음 볼륨 기본값 (0.0 ~ 1.0)

### FMOD Studio에서 VCA 경로 확인 방법
1. FMOD Studio를 열고 프로젝트를 엽니다
2. Mixer 탭으로 이동합니다
3. VCA를 찾아 우클릭 → "Copy Path"를 선택합니다
4. 복사된 경로를 인스펙터의 해당 필드에 붙여넣습니다

### 주의사항
- VCA 경로가 잘못되면 경고 메시지가 출력되지만 게임은 계속 실행됩니다
- VCA가 없어도 슬라이더는 작동하지만 실제 볼륨 조절은 되지 않습니다

---

## 2. GameMenuManager 설정

### 위치
- GameScene의 UI 계층 구조에 `GameMenuManager` 컴포넌트 추가
- 메뉴 UI가 있는 GameObject에 추가하는 것을 권장

### 인스펙터 설정 항목

#### Menu UI References
- **Menu Open Button**: 메뉴를 열기 위한 버튼 (Button 컴포넌트)
- **Main Panel**: 메뉴의 메인 판넬 (GameObject)
- **Continue Button**: 계속하기 버튼 (Button 컴포넌트)
- **Return To Title Button**: 타이틀 화면 복귀 버튼 (Button 컴포넌트)
- **Quit Game Button**: 게임 종료 버튼 (Button 컴포넌트)

#### Volume Sliders
- **Master Volume Slider**: 마스터 볼륨 조절 슬라이더 (Slider 컴포넌트)
  - Min Value: 0
  - Max Value: 1
  - Whole Numbers: false (체크 해제)
  
- **SFX Volume Slider**: 효과음 볼륨 조절 슬라이더 (Slider 컴포넌트)
  - Min Value: 0
  - Max Value: 1
  - Whole Numbers: false (체크 해제)
  
- **Music Volume Slider**: 음악 볼륨 조절 슬라이더 (Slider 컴포넌트)
  - Min Value: 0
  - Max Value: 1
  - Whole Numbers: false (체크 해제)

#### Volume Text Displays
- **Master Volume Text**: 마스터 볼륨 값을 표시할 텍스트 (TMP_Text 컴포넌트)
- **SFX Volume Text**: 효과음 볼륨 값을 표시할 텍스트 (TMP_Text 컴포넌트)
- **Music Volume Text**: 음악 볼륨 값을 표시할 텍스트 (TMP_Text 컴포넌트)

#### Audio
- **Menu Open Sound**: 메뉴 열릴 때 재생할 사운드 (EventReference)
- **Menu Close Sound**: 메뉴 닫힐 때 재생할 사운드 (EventReference)
- **Button Click Sound**: 버튼 클릭 시 재생할 사운드 (EventReference)

### UI 구성 예시

```
MenuPanel (GameObject)
├── GameMenuManager (컴포넌트)
├── MenuOpenButton (Button)
└── MainPanel (GameObject)
    ├── ContinueButton (Button)
    ├── VolumeSettings (GameObject)
    │   ├── MasterVolume (GameObject)
    │   │   ├── MasterVolumeSlider (Slider)
    │   │   └── MasterVolumeText (TMP_Text)
    │   ├── SFXVolume (GameObject)
    │   │   ├── SFXVolumeSlider (Slider)
    │   │   └── SFXVolumeText (TMP_Text)
    │   └── MusicVolume (GameObject)
    │       ├── MusicVolumeSlider (Slider)
    │       └── MusicVolumeText (TMP_Text)
    ├── ReturnToTitleButton (Button)
    └── QuitGameButton (Button)
```

### 슬라이더 설정
1. 각 슬라이더의 **Min Value**를 `0`으로 설정
2. 각 슬라이더의 **Max Value**를 `1`로 설정
3. **Whole Numbers** 옵션을 체크 해제 (소수점 값 허용)

### 텍스트 설정
1. 각 볼륨 텍스트는 TMP_Text 컴포넌트를 사용
2. 텍스트는 자동으로 "0%" ~ "100%" 형식으로 표시됨
3. 초기 텍스트는 설정하지 않아도 됨 (스크립트가 자동 업데이트)

---

## 3. 설정 순서

1. **FMODVolumeController 설정**
   - GameScene에 GameObject 생성 또는 기존 GameObject 선택
   - `FMODVolumeController` 컴포넌트 추가
   - FMOD Studio에서 VCA 경로 확인 후 입력
   - 기본 볼륨 값 설정 (선택사항)

2. **GameMenuManager 설정**
   - 메뉴 UI GameObject 선택
   - `GameMenuManager` 컴포넌트 추가
   - 모든 UI 참조 연결 (버튼, 슬라이더, 텍스트)
   - 사운드 이벤트 참조 연결 (선택사항)

3. **슬라이더 및 텍스트 확인**
   - 각 슬라이더의 Min/Max 값 확인 (0 ~ 1)
   - 각 텍스트 컴포넌트가 올바르게 연결되었는지 확인

---

## 4. 테스트 방법

1. 게임 실행 후 메뉴 열기 (Escape 키 또는 메뉴 버튼)
2. 각 슬라이더를 움직여보며 텍스트가 실시간으로 업데이트되는지 확인
3. 볼륨이 실제로 변경되는지 확인 (FMOD VCA가 올바르게 설정된 경우)
4. 게임을 종료하고 다시 실행하여 볼륨 값이 저장되었는지 확인

---

## 5. 문제 해결

### 볼륨이 변경되지 않는 경우
- FMOD Studio에서 VCA 경로가 올바른지 확인
- FMOD 프로젝트가 빌드되었는지 확인
- Console에서 VCA 관련 경고 메시지 확인

### 텍스트가 업데이트되지 않는 경우
- 인스펙터에서 텍스트 참조가 올바르게 연결되었는지 확인
- TMP_Text 컴포넌트가 있는지 확인
- 슬라이더의 onValueChanged 이벤트가 정상 작동하는지 확인

### 볼륨 값이 저장되지 않는 경우
- PlayerPrefs가 정상적으로 작동하는지 확인
- 게임 종료 시 PlayerPrefs.Save()가 호출되는지 확인
