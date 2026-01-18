# 벤치마킹을 통한 타일맵 제작 시스템 UI/UX 개선 방안 보고서

## 1. 개요
본 문서는 `SpatialTileBuilder`의 현재 단일 데이터베이스 연결 방식이 가진 한계를 극복하고, **다중 소스(벡터, 라스터, 복수 DB)**를 통합하여 타일맵을 제작할 수 있도록, QGIS, Mapbox Studio, ArcGIS Pro 등 상용 GIS 소프트웨어의 아키텍처를 벤치마킹하여 구체적인 개선 방안을 제시하는 것을 목적으로 한다.

---

## 2. 현황 및 문제점 분석 (AS-IS)
*   **단일 연결 의존성**: 현재 시스템은 앱 시작 시 한 번의 DB 연결(`ConnectionInfo`)을 맺고, 해당 DB 내의 테이블만 로드할 수 있음.
*   **이종 데이터 혼합 불가**: PostGIS A의 벡터 데이터와 PostGIS B의 벡터 데이터, 혹은 로컬의 GeoTIFF(라스터)를 하나의 타일셋으로 합치는 작업이 불가능함.
*   **스타일-데이터 결합**: 스타일 설정이 런타임 객체에 종속적이며, 데이터 소스와 분리된 "프로젝트" 개념이 약함.

---

## 3. 타 GIS 소프트웨어 벤치마킹 (Benchmarking)

### 3.1. QGIS (Quantum GIS)
*   **소스 관리 (Browser Panel)**:
    *   `Browser Panel`을 통해 PostGIS, Oracle, MSSQL, GeoPackage, 로컬 파일 시스템 등 다양한 소스를 트리 형태로 탐색.
    *   **핵심**: 연결(Connection)은 개별적으로 관리되며, 프로젝트는 이 연결들에 대한 '참조'만 가짐.
*   **레이어 관리 (Layers Panel)**:
    *   서로 다른 소스(예: PostGIS 레이어 + 위성사진 Raster)를 하나의 `Layers Panel`에 드래그 앤 드롭으로 쌓아 올림(Stacking).
    *   **Render Order**: 리스트의 순서가 곧 렌더링 순서(Z-index).
*   **스타일링 (Layer Properties)**:
    *   각 레이어마다 독립적인 `Symbology` 탭 존재.
    *   QML/SLD 파일로 스타일만 별도 저장/로드 가능.

### 3.2. Mapbox Studio
*   **Sources vs Styles 분리**:
    *   **Sources (Tilesets)**: 데이터를 먼저 업로드하여 'Vector Tileset'으로 변환. (데이터 전처리)
    *   **Layers**: 스타일 에디터에서 소스를 불러와 스타일링 레이어를 생성.
    *   **핵심**: 하나의 스타일 프로젝트에서 여러 개의 Source를 참조할 수 있음. (Composite Source)
*   **컴포넌트 기반 스타일링**:
    *   도로, 건물, 물 등 성격이 다른 데이터를 그룹화하여 관리.

### 3.3. GeoServer
*   **Store & Layer 개념**:
    *   **Workspace**: 프로젝트의 최상위 단위.
    *   **Store**: 실제 데이터 연결(Connection). (예: PostGIS_Main, Shapefile_Dir)
    *   **Layer**: Store에서 발행한 개별 데이터셋.
    *   **Layer Group**: 여러 레이어를 묶어 하나의 WMS 엔드포인트로 제공. 이것이 '타일 생성'의 단위가 됨.

---

## 4. UI/UX 개선 방안 (TO-BE Proposal)

현재의 선형적인(Linear) 마법사 방식(연결 -> 선택 -> 스타일 -> 생성)에서 벗어나, **"프로젝트 기반 저작 도구(Project-based Authoring Tool)"** 형태로의 전환이 필요함.

### 4.1. 아키텍처 재설계: "소스(Store) - 레이어(Layer) - 프로젝트(Project)"
1.  **DataSource Manager (데이터 원본 관리자)**
    *   기존의 `ConnectionWizard`를 격상시킴.
    *   앱 내에서 여러 개의 커넥션을 등록할 수 있는 전역 패널 제공.
    *   지원 타입: `PostGIS`, `Shapefile(Local)`, `GeoTIFF(Raster)`, `MBTiles(Reference)`
    *   *예: [DB_Seoul 연결], [DB_Busan 연결], [Satelite_Image.tif]를 각각 등록.*

2.  **Layer Composer (레이어 구성 패널)**
    *   'Map Canvas' 좌측에 포토샵/QGIS와 유사한 레이어 목록 패널 배치.
    *   DataSource Manager에서 테이블/파일을 드래그하여 지도에 추가.
    *   서로 다른 DB의 테이블을 하나의 맵 뷰에서 동시에 조회.

3.  **Project State (프로젝트 저장)**
    *   단순 스타일 JSON 저장이 아닌, `.sproj` (가칭) 형태의 프로젝트 파일 정의.
    *   포함 내용:
        *   등록된 DataSource 목록 (접속 정보 포함/제외 선택)
        *   구성된 Layer 목록 및 순서
        *   각 Layer별 적용된 Style 정보
        *   타일링 설정 (Zoom, BBox, Format)

### 4.2. 주요 화면 UI 개선안

#### A. 메인 작업 화면 (Main Workspace)
*   **좌측 패널 (Layer & Source)**:
    *   **탭 1 (Layers)**: 현재 맵에 올라와 있는 레이어 목록. 순서 변경(Drag-n-drop), ON/OFF, 투명도 조절.
    *   **탭 2 (Sources)**: 등록된 DB 연결 및 로컬 파일 트리. 여기서 레이어 탭으로 데이터를 끌어옴.
*   **중앙 패널 (Map Canvas)**:
    *   구성된 레이어들이 실시간으로 렌더링되는 미리보기 창. (현재 `StylePreviewPage`의 확장)
    *   기존 단순 이미지 뷰어에서 벗어나, 마우스 휠 줌/팬, 피처 클릭 시 속성 정보 확인(Identify) 기능 추가.
*   **우측 패널 (Style Editor)**:
    *   **고급 스타일링 (Rule-based Styling)**:
        *   단순 '단일 심볼(Single Symbol)' 방식을 넘어, **컬럼 값(Domain)에 따른 스타일 분기** 기능 추가.
        *   **Categorized**: 특정 컬럼의 고유값(예: 건물 용도, 도로 종류)에 따라 색상/심볼 다르게 적용.
        *   **Graduated**: 수치 컬럼(예: 인구수, 높이)의 범위에 따라 단계 구분도(Choropleth) 표현.
    *   좌측에서 선택한 레이어의 상세 스타일 속성창.
    *   (기존 기능 유지하되, 벡터/라스터 타입에 따라 UI 분기 처리)
        *   **Vector**: Fill, Stroke, Label, Point.
        *   **Raster**: Band Rendering, Hillshade, Opacity, Blend Mode.

#### B. 타일 생성 설정 화면 (Export)
*   현재 `RegionSelection` 단계를 'Export' 기능으로 분리.
*   프로젝트 전체(모든 Visible 레이어)를 대상으로 할지, 데이터 영역(Extent)을 자동으로 계산하여 제안.

---

## 5. 단계별 구현 로드맵

### Phase 1: Muti-Connection 지원 (Foundation)
*   `IPostGISConnectionService`를 `IDataSourceService`로 추상화.
*   `ConnectionInfo`를 리스트 형태(`Dictionary<string, ConnectionInfo>`)로 관리하도록 상태(`ProjectStateService`) 변경.
*   UI: '데이터 추가' 팝업을 통해 여러 DB 연결을 리스트에 추가하고 전환할 수 있게 구현.

### Phase 2: 레이어 합성 엔진 (Rendering)
*   `MapnikRenderer`가 단일 Connection이 아닌, 각 `LayerStyle` 객체 내에 포함된 `DataSourceId`를 참조하여 서로 다른 소스에서 데이터를 가져오게 수정.
*   서로 다른 좌표계(SRID)를 가진 소스들이 `EPSG:3857` 캔버스 위에서 정확히 중첩되도록 `PostGIS` 쿼리 내 `ST_Transform` 로직 강화.

### Phase 3: 라스터(Raster) 지원
*   `GDAL` 혹은 `Mapnik RasterSymbolizer` 연동.
*   로컬 GeoTIFF 파일을 읽어 배경 지도로 까는 기능 추가.

---

## 6. 최종 목표 레이아웃 (Target UI Wireframe)

제안하는 시스템의 최종 UI 구성도는 다음과 같습니다.

```text
+-----------------------------------------------------------------------------------+
|  [Menu Bar] File(프로젝트)  Datasource(연결)  View  Export(타일생성)  Help        |
+-----------------------+-----------------------------------------+-----------------+
| [Browser / Sources]   |  [Map Canvas (Main View)]               | [Style / Prop]  |
|                       |                                         |                 |
|  ▼ PostGIS_Main       |                                         |  Selected:      |
|     - public.blds     |                                         |   [Layer 1]     |
|     - public.roads    |                                         |                 |
|  ▼ Local_Files        |           (Map Interaction Area)        +-----------------+
|     - satellite.tif   |                                         | [Fill Style]    |
|     |  | Color: ■ #F00 |
| --- ||  Opacity: ──○─  |
| [Layers (Stack)]      |                                         |                 |
|                       |                                         | [Stroke Style]  |
|  ≡ [v] 1. Buildings   |                                         |  Width: 1.5px   |
|  ≡ [v] 2. Roads       |                                         |                 |
|  ≡ [ ] 3. Water       |                                         | [Label Style]   |
|  ≡ [v] 4. Satellite   |          [Zoom Controls]                |  Column: name   |
|                       |                                         |  Font: Arial    |
| (+ Add / - Remove)    |  [Scale Bar]      [Coords: 127, 37]     |                 |
+-----------------------+-----------------------------------------+-----------------+
| [Status Bar] Ready.                   [Processing: 0%]          Threads: 8 (Auto) |
+-----------------------------------------------------------------------------------+
```

### 구역별 핵심 기능
1.  **좌측 패널 (Data Management)**:
    *   **Browser**: 등록된 모든 데이터 소스(DB, 파일)를 트리 구조로 탐색. 더블 클릭으로 레이어 추가.
    *   **Layers**: 현재 프로젝트에 포함된 레이어의 렌더링 순서(Z-Order) 및 가시성(On/Off) 제어.
2.  **중앙 패널 (Visualization)**:
    *   **Map Canvas**: 벡터와 라스터가 합성된 최종 결과를 실시간 확인.
    *   **Interaction**: 마우스 휠 줌/팬, Feature 정보 조회, 타일 생성 영역(BBox) 드래그 선택.
3.  **우측 패널 (Properties)**:
    *   **Style Editor**: 선택한 레이어 타입(Point/Line/Polygon/Raster)에 맞는 동적 스타일 옵션 제공.
    *   **Filter**: SQL Where 절을 UI로 구성하여 데이터 필터링 기능 제공.

---

## 7. 결론
현재의 '단일 파이프라인' 방식은 초기 접근성은 좋으나, 복잡한 지도 제작 요구사항(배경지도+주제도 중첩 등)을 충족하기 어렵습니다. 위에서 제시한 **"소스-레이어 분리"** 및 **"프로젝트 기반 관리"** 방식으로의 전환은 `SpatialTileBuilder`를 단순 유틸리티에서 **전문적인 타일맵 저작 도구**로 발전시키는 핵심 UI/UX 전략이 될 것입니다.
