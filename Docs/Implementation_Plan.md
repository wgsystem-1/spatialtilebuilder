# 구현 계획: SpatialTileBuilder UI/UX 현대화

이 계획은 `SpatialTileBuilder`를 단순한 선형 마법사 기반 유틸리티에서, 다중 데이터 소스, 규칙 기반 스타일링, 통합 작업 공간을 지원하는 전문적인 프로젝트 기반 저작 도구(`.sproj`)로 변환하기 위한 단계별 로드맵을 설명합니다.

## 목표 (Goals)
1.  **다중 소스 지원 (Multi-Source Support)**: PostGIS, Shapefile, Raster 소스를 하나의 지도에 통합.
2.  **프로젝트 기반 워크플로우 (Project-Based Workflow)**: 전체 프로젝트 상태를 저장/불러오기 위한 `.sproj` 파일 도입.
3.  **현대적인 레이아웃 (Modern Layout)**: 3-pane 레이아웃 (브라우저/레이어 - 캔버스 - 속성) 구현.
4.  **고급 스타일링 (Advanced Styling)**: 벡터 레이어에 대한 규칙 기반 (Categorized) 스타일링 지원.
5.  **모든 기능 구현**: 기능구현은 임시적이거나 껍데기만 구현하지 않고 모든 기능은 제한없이 구현한다.

---

## 5단계 구현 로드맵 (5-Step Implementation Roadmap)

### Step 1: 기초 작업 - 프로젝트 및 소스 관리
**목표**: 애플리케이션을 단일 전역 연결에서 분리하고 영속적인 "프로젝트" 상태를 도입합니다.
- [ ] **1.1. 프로젝트 모델 생성**:
    - `StyleConfiguration`을 대체하는 `ProjectConfiguration` 레코드 정의.
    - 여러 소스에 대한 연결 문자열/경로를 저장하는 `DataSourceConfig` 정의.
    - `SourceId`를 참조하고 스타일 데이터를 보유하는 `LayerConfig` 정의.
- [ ] **1.2. 서비스 리팩토링**:
    - 활성 프로젝트를 관리하고 `.sproj` (JSON)를 로드/저장하는 `ProjectService` 생성.
    - `PostGISConnectionService`를 소스별로 인스턴스화하거나 연결 풀 딕셔너리를 관리하도록 리팩토링.
    - PostGIS 대 파일 기반 소스 생성을 처리하는 `DataSourceFactory` 생성.
- [ ] **1.3. UI - 소스 브라우저**:
    - 등록된 소스를 나열하는 "소스 브라우저" ViewModel/View 구현.
    - PostGIS 및 파일(Shp/GeoTiff)에 대한 "소스 추가" 대화 상자 추가.

### Step 2: 핵심 UI - 메인 작업 공간 레이아웃
**목표**: 선형적인 NavigationService 흐름을 도킹/크기 조절 가능한 패널이 있는 단일 메인 창으로 교체합니다.
- [ ] **2.1. 메인 윈도우 셸 (Main Window Shell)**:
    - 특정 Grid 레이아웃(좌, 중, 우, 하)을 가진 `MainWorkspaceView.xaml` 생성.
    - 메뉴 모음 (파일, 보기, 도움말) 구현.
- [ ] **2.2. 통합 맵 캔버스 (Integrated Map Canvas)**:
    - `StylePreviewPage` 로직을 작업 공간의 중심이 되는 재사용 가능한 `MapCanvasView` 컨트롤로 이식.
    - 팬/줌 상호작용 (마우스 휠, 드래그) 활성화.
- [ ] **2.3. 레이어 관리 패널 (Layer Management Panel)**:
    - 드래그 앤 드롭 순서 변경을 지원하는 `LayerListViewModel` 생성.
    - 레이어 가시성 토글을 맵 캔버스 렌더러와 연결.

### Step 3: 렌더링 엔진 - 합성 및 라스터
**목표**: 렌더러가 여러 소스의 레이어를 올바른 순서로 그릴 수 있도록 합니다.
- [ ] **3.1. 추상 데이터 공급자 (Abstract Data Provider)**:
    - `GetGeometriesAsync` 및 `GetImageAsync` 메서드를 가진 `ILayerDataProvider` 인터페이스 생성.
    - `PostGISDataProvider`, `ShapefileDataProvider`, `RasterDataProvider` 구현.
- [ ] **3.2. 합성 렌더러 업그레이드 (Composite Renderer Upgrade)**:
    - 프로젝트의 레이어 목록을 순회하도록 `MapnikRenderer` (또는 Mock) 업데이트.
    - 각 레이어에 대해 `SourceId`를 통해 공급자를 확인하고 데이터를 가져옴.
    - WebMercator가 아닌 소스에 대해 맵 캔버스와 일치하도록 `ST_Transform` 또는 좌표 재투영 구현.

### Step 4: 고급 스타일링 - 규칙 기반 엔진
**목표**: "분류값 사용(Categorized)" 및 "규칙 기반(Rule-based)" 스타일링을 구현합니다.
- [ ] **4.1. 확장된 스타일 모델**:
    - `LayerStyle`이 `StyleRule` 객체 목록을 포함하도록 리팩토링.
    - `StyleRule`: `{ FilterExpression, Symbolizer }`
- [ ] **4.2. 스타일 편집기 UI**:
    - 다중 규칙을 지원하도록 속성 패널 업그레이드.
    - "규칙 편집기" 대화 상자 생성 (필터 입력 + 심볼 설정).
    - 고유 컬럼 값에서 규칙을 자동 생성하는 "분류(Classify)" 버튼 구현.

### Step 5: 내보내기 및 최종화
**목표**: 새로운 프로젝트 시스템을 사용하여 타일 생성 출력을 다시 통합합니다.
- [ ] **5.1. 내보내기 대화 상자**:
    - "타일 내보내기"를 위한 모달 대화 상자 생성 (`RegionSelectionPage` 대체).
    - BBox 선택을 현재 맵 캔버스 범위(Extent)와 바인딩.
- [ ] **5.2. 통합 테스트**:
    - `.sproj` 저장/불러오기 검증.
    - 혼합 소스(PostGIS + Shapefile) 렌더링 검증.
    - 내보내기 출력의 정확성 검증.

---

## 기술적 고려사항 (Technical Considerations)
- **의존성 주입 (Dependency Injection)**: 다양한 소스 유형에 대한 공급자를 해결하기 위해 DI를 적극 활용.
- **Async/Await**: 여러 대용량 소스를 로드할 때 UI 응답성을 위해 필수적.
- **SkiaSharp**: 렌더링 엔진으로 계속 사용; 다른 소스에서 병렬 타일을 렌더링할 때 스레드 안전성 보장.
