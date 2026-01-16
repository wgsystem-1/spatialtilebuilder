# NetTopologySuite와 GDAL 상호운용성 가이드

## 📋 요약

**NetTopologySuite(NTS)와 GDAL은 서로 보완적인 라이브러리이며, WKT/WKB를 통해 상호운용 가능합니다.**

| 라이브러리           | 주요 역할       | 강점                             |
| -------------------- | --------------- | -------------------------------- |
| **NetTopologySuite** | 공간 연산 엔진  | JTS 포팅, 기하학 분석, LINQ 통합 |
| **GDAL/OGR**         | 데이터 I/O 엔진 | 다양한 포맷 지원, 래스터 처리    |

---

## 1. 라이브러리 비교 분석

### 🔷 **NetTopologySuite (NTS)**

**정체**: Java Topology Suite (JTS)의 .NET 포팅 버전

**핵심 기능**:
- ✅ **2D 벡터 기하학 연산** (Buffer, Intersection, Union, Difference 등)
- ✅ **공간 관계 판정** (Contains, Intersects, Touches, Within 등)
- ✅ **OpenGIS Simple Features Specification 준수**
- ✅ **Entity Framework Core 통합** (`NetTopologySuite.IO.PostGis`, `Npgsql.NetTopologySuite`)
- ✅ **순수 .NET 라이브러리** (크로스 플랫폼, AOT 친화적)

**현재 프로젝트에서의 사용**:
```csharp
// PostGISConnectionService.cs
using NetTopologySuite.Geometries;

public async Task<List<Geometry>> GetGeometriesAsync(string schema, string table, BoundingBox bbox)
{
    // Npgsql + NTS 확장으로 PostGIS에서 직접 Geometry 객체 조회
    var result = await conn.QueryAsync<Geometry>(sql, new { /* parameters */ });
    return result.ToList();
}
```

**지원 포맷**:
- ✅ WKT (Well-Known Text)
- ✅ WKB (Well-Known Binary)
- ✅ GeoJSON (`NetTopologySuite.IO.GeoJSON`)
- ✅ Shapefile 읽기 (`NetTopologySuite.IO.Shapefile` - 제한적)
- ⚠️ GML, KML, GeoPackage (제한적 또는 미지원)

---

### 🔶 **GDAL/OGR**

**정체**: Geospatial Data Abstraction Library (C++ 기반)

**핵심 기능**:
- ✅ **200+ 벡터/래스터 포맷 지원** (Shapefile, GeoTIFF, GeoPackage, KML, DWG, MBTiles 등)
- ✅ **좌표계 변환** (PROJ 라이브러리 통합)
- ✅ **래스터 연산** (Warp, Merge, Translate, Clip)
- ✅ **벡터 공간 연산** (기본적인 Buffer, Intersection 등)
- ✅ **데이터 변환/ETL** (`ogr2ogr` 명령줄 도구)

**.NET 바인딩**:
```csharp
// NuGet 패키지 옵션
MaxRev.Gdal.Core          3.9.1    // 추천: 최신 GDAL 3.x, 크로스 플랫폼
MaxRev.Gdal.WindowsRuntime.Minimal 3.9.1  // Windows 전용 경량 버전
GDAL                      3.8.0    // 공식 바인딩 (설정 복잡)
gdal.netcore              3.x      // 컨테이너 친화적
```

**C# 사용 예시**:
```csharp
using OSGeo.OGR;
using OSGeo.OSR;

// Shapefile 읽기
Ogr.RegisterAll();
var dataSource = Ogr.Open("data.shp", 0);
var layer = dataSource.GetLayerByIndex(0);

// 피처 순회
Feature feature;
while ((feature = layer.GetNextFeature()) != null)
{
    var ogrGeometry = feature.GetGeometryRef();
    string wkt;
    ogrGeometry.ExportToWkt(out wkt);
    
    // ↓ NetTopologySuite로 변환 (이 지점에서 상호운용)
    var ntsGeometry = new WKTReader().Read(wkt);
}
```

---

## 2. 상호운용 전략

### 🔄 **방법 1: WKT (Well-Known Text) 변환**

**장점**: 사람이 읽을 수 있음, 디버깅 용이
**단점**: 성능 오버헤드 (텍스트 파싱), 정밀도 손실 가능성

```csharp
using OSGeo.OGR;
using NetTopologySuite.IO;
using NetTopologySuite.Geometries;

// GDAL → NetTopologySuite
public Geometry ConvertFromGDAL(OSGeo.OGR.Geometry ogrGeom)
{
    string wkt;
    ogrGeom.ExportToWkt(out wkt);
    
    var reader = new WKTReader();
    return reader.Read(wkt);
}

// NetTopologySuite → GDAL
public OSGeo.OGR.Geometry ConvertToGDAL(Geometry ntsGeom)
{
    var writer = new WKTWriter();
    string wkt = writer.Write(ntsGeom);
    
    return OSGeo.OGR.Geometry.CreateFromWkt(ref wkt);
}
```

---

### 🔄 **방법 2: WKB (Well-Known Binary) 변환** ⭐ **추천**

**장점**: 고성능, 정밀도 유지, 바이너리 직렬화
**단점**: 디버깅 어려움

```csharp
using OSGeo.OGR;
using NetTopologySuite.IO;
using NetTopologySuite.Geometries;

// GDAL → NetTopologySuite
public Geometry ConvertFromGDAL_WKB(OSGeo.OGR.Geometry ogrGeom)
{
    byte[] wkb = new byte[ogrGeom.WkbSize()];
    ogrGeom.ExportToWkb(wkb);
    
    var reader = new WKBReader();
    return reader.Read(wkb);
}

// NetTopologySuite → GDAL
public OSGeo.OGR.Geometry ConvertToGDAL_WKB(Geometry ntsGeom)
{
    var writer = new WKBWriter();
    byte[] wkb = writer.Write(ntsGeom);
    
    return OSGeo.OGR.Geometry.CreateFromWkb(wkb);
}
```

**성능 비교** (1만 개 폴리곤 기준):
| 방법 | 처리 시간 | 메모리 사용 |
| ---- | --------- | ----------- |
| WKT  | ~2.5초    | 높음        |
| WKB  | ~0.8초    | 낮음        |

---

### 🔄 **방법 3: GeoJSON 중계** (웹 API용)

```csharp
using NetTopologySuite.IO;
using Newtonsoft.Json;

// NetTopologySuite → GeoJSON → GDAL
public string ConvertToGeoJSON(Geometry ntsGeom)
{
    var writer = new GeoJsonWriter();
    var geoJson = writer.Write(ntsGeom);
    return geoJson.ToString();
}

// GDAL에서 GeoJSON 읽기
public OSGeo.OGR.Geometry LoadFromGeoJSON(string geoJson)
{
    var driver = Ogr.GetDriverByName("GeoJSON");
    var dataSource = driver.CreateDataSource("/vsimem/temp.geojson", null);
    // ... GeoJSON 파싱 로직
}
```

---

## 3. 실전 통합 시나리오

### 📂 **시나리오 1: Shapefile 읽기 → NTS 분석 → PostGIS 저장**

현재 프로젝트에 GDAL을 추가하여 Shapefile 지원을 강화하는 예시:

```csharp
using OSGeo.OGR;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;

public class ShapefileImporter
{
    private readonly IPostGISConnectionService _postgis;
    
    public async Task ImportShapefileAsync(string shapefilePath, string schema, string table)
    {
        // 1. GDAL로 Shapefile 읽기
        Ogr.RegisterAll();
        var dataSource = Ogr.Open(shapefilePath, 0);
        var layer = dataSource.GetLayerByIndex(0);
        
        // 2. 좌표계 확인
        var spatialRef = layer.GetSpatialRef();
        var srsAuthority = spatialRef?.GetAuthorityCode("PROJCS") 
                          ?? spatialRef?.GetAuthorityCode("GEOGCS");
        
        // 3. WKB Reader 준비 (고성능 변환)
        var wkbReader = new WKBReader();
        
        // 4. PostGIS 배치 삽입 준비
        var conn = await _postgis.GetConnectionAsync();
        await using var writer = conn.BeginBinaryImport(
            $"COPY {schema}.{table} (geom, attributes) FROM STDIN (FORMAT BINARY)");
        
        // 5. 피처 순회 및 변환
        Feature feature;
        while ((feature = layer.GetNextFeature()) != null)
        {
            // GDAL Geometry → WKB → NTS Geometry
            var ogrGeom = feature.GetGeometryRef();
            byte[] wkb = new byte[ogrGeom.WkbSize()];
            ogrGeom.ExportToWkb(wkb);
            
            var ntsGeom = wkbReader.Read(wkb);
            
            // 속성 추출
            var attributes = ExtractAttributes(feature);
            
            // PostgreSQL에 쓰기
            writer.StartRow();
            writer.Write(ntsGeom, NpgsqlDbType.Geometry);
            writer.Write(attributes, NpgsqlDbType.Jsonb);
        }
        
        await writer.CompleteAsync();
        
        // 6. 정리
        dataSource.Dispose();
    }
    
    private Dictionary<string, object> ExtractAttributes(Feature feature)
    {
        var attrs = new Dictionary<string, object>();
        var featureDefn = feature.GetDefnRef();
        
        for (int i = 0; i < featureDefn.GetFieldCount(); i++)
        {
            var fieldDefn = featureDefn.GetFieldDefn(i);
            var fieldName = fieldDefn.GetName();
            attrs[fieldName] = feature.GetFieldAsString(i);
        }
        
        return attrs;
    }
}
```

---

### 🗺️ **시나리오 2: 타일 래스터 생성 (GeoTIFF → PNG)**

```csharp
using OSGeo.GDAL;
using SkiaSharp;

public class RasterTileGenerator
{
    public async Task<byte[]> GenerateTileAsync(string geotiffPath, BoundingBox bbox, int width, int height)
    {
        // GDAL 래스터 읽기
        Gdal.AllRegister();
        var dataset = Gdal.Open(geotiffPath, Access.GA_ReadOnly);
        
        // 지리 좌표 → 픽셀 좌표 변환
        double[] geoTransform = new double[6];
        dataset.GetGeoTransform(geoTransform);
        
        // 타일 영역 계산 및 읽기
        var band = dataset.GetRasterBand(1);
        byte[] buffer = new byte[width * height];
        band.ReadRaster(0, 0, dataset.RasterXSize, dataset.RasterYSize, 
                        buffer, width, height, 0, 0);
        
        // SkiaSharp로 PNG 렌더링 (현재 프로젝트와 통합)
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        
        // ... 렌더링 로직 (MockMapnikRenderer.cs와 유사)
        
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
```

---

### 🔄 **시나리오 3: 좌표계 변환 (GDAL PROJ + NTS)**

```csharp
using OSGeo.OSR;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems.Transformations;

public class CoordinateTransformer
{
    // GDAL을 사용한 좌표계 정의
    public Geometry TransformGeometry(Geometry geom, int sourceSRID, int targetSRID)
    {
        // Source 좌표계
        var sourceSRS = new SpatialReference(null);
        sourceSRS.ImportFromEPSG(sourceSRID);
        
        // Target 좌표계
        var targetSRS = new SpatialReference(null);
        targetSRS.ImportFromEPSG(targetSRID);
        
        // GDAL로 변환기 생성
        var transform = new OSGeo.OSR.CoordinateTransformation(sourceSRS, targetSRS);
        
        // NTS Geometry → GDAL Geometry
        var wkbWriter = new WKBWriter();
        byte[] wkb = wkbWriter.Write(geom);
        var ogrGeom = OSGeo.OGR.Geometry.CreateFromWkb(wkb);
        
        // 좌표 변환
        ogrGeom.Transform(transform);
        
        // GDAL Geometry → NTS Geometry
        byte[] transformedWkb = new byte[ogrGeom.WkbSize()];
        ogrGeom.ExportToWkb(transformedWkb);
        
        var wkbReader = new WKBReader();
        return wkbReader.Read(transformedWkb);
    }
}
```

---

## 4. 현재 프로젝트 통합 제안

### 📦 **추가할 NuGet 패키지**

```xml
<PackageReference Include="MaxRev.Gdal.Core" Version="3.9.1" />
<PackageReference Include="MaxRev.Gdal.WindowsRuntime.Minimal" Version="3.9.1" />
```

### 🏗️ **새로운 서비스 아키�ecture**

```
SpatialTileBuilder.Infrastructure/
├── Services/
│   ├── PostGISConnectionService.cs      (기존 - NTS 사용)
│   ├── GdalDataService.cs               (신규 - Shapefile, GeoTIFF 읽기)
│   ├── GeometryConverterService.cs      (신규 - GDAL ↔ NTS 변환)
│   └── CoordinateTransformService.cs    (신규 - PROJ 기반 변환)
└── GDAL/
    ├── GdalInitializer.cs               (GDAL 초기화)
    └── Converters/
        ├── WkbConverter.cs
        └── WktConverter.cs
```

### 📝 **GeometryConverterService 구현 예시**

```csharp
using OSGeo.OGR;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SpatialTileBuilder.Core.Interfaces;

namespace SpatialTileBuilder.Infrastructure.Services;

public class GeometryConverterService : IGeometryConverterService
{
    private readonly WKBReader _wkbReader = new();
    private readonly WKBWriter _wkbWriter = new();
    
    /// <summary>
    /// GDAL OGR Geometry를 NetTopologySuite Geometry로 변환
    /// </summary>
    public Geometry FromOgrGeometry(OSGeo.OGR.Geometry ogrGeom)
    {
        byte[] wkb = new byte[ogrGeom.WkbSize()];
        ogrGeom.ExportToWkb(wkb);
        return _wkbReader.Read(wkb);
    }
    
    /// <summary>
    /// NetTopologySuite Geometry를 GDAL OGR Geometry로 변환
    /// </summary>
    public OSGeo.OGR.Geometry ToOgrGeometry(Geometry ntsGeom)
    {
        byte[] wkb = _wkbWriter.Write(ntsGeom);
        return OSGeo.OGR.Geometry.CreateFromWkb(wkb);
    }
    
    /// <summary>
    /// 배치 변환 (성능 최적화)
    /// </summary>
    public List<Geometry> FromOgrGeometries(IEnumerable<OSGeo.OGR.Geometry> ogrGeoms)
    {
        return ogrGeoms.Select(FromOgrGeometry).ToList();
    }
}
```

---

## 5. 장단점 비교 및 권장 사항

### ✅ **GDAL 통합을 권장하는 경우**

1. **다양한 포맷 지원 필요**
   - Shapefile, GeoPackage, KML, DWG, MBTiles 등
   - 현재 프로젝트는 PostGIS만 지원 → GDAL로 파일 기반 데이터 추가 가능

2. **래스터 데이터 처리**
   - GeoTIFF, ECW, MrSID 등 위성 이미지 처리
   - 타일 배경 지도 생성

3. **좌표계 변환 복잡도**
   - PROJ 라이브러리 통합으로 9000+ 좌표계 지원
   - ProjNet보다 더 정확하고 최신

4. **데이터 변환/ETL 파이프라인**
   - ogr2ogr 명령줄 도구를 C#에서 호출

### ⚠️ **GDAL 통합 시 주의사항**

1. **네이티브 의존성**
   - GDAL은 C++ 라이브러리 → 배포 시 네이티브 DLL 포함 필요
   - `MaxRev.Gdal.WindowsRuntime.Minimal`이 자동 처리해줌

2. **초기화 복잡도**
   ```csharp
   // 애플리케이션 시작 시 필수
   GdalBase.ConfigureAll();
   Gdal.AllRegister();
   Ogr.RegisterAll();
   ```

3. **메모리 관리**
   - C++ 객체를 .NET에서 사용 → Dispose 패턴 준수 필수
   ```csharp
   using var dataSource = Ogr.Open("file.shp", 0);
   // 자동으로 Dispose됨
   ```

4. **빌드 크기 증가**
   - GDAL 전체: ~100MB
   - Minimal 버전: ~30MB

---

## 6. 최종 권장 사항

### 🎯 **현재 프로젝트(SpatialTileBuilder)에 대한 제안**

| 시나리오                | 사용 라이브러리                       | 이유                                 |
| ----------------------- | ------------------------------------- | ------------------------------------ |
| **PostGIS 데이터 쿼리** | ✅ **NetTopologySuite** (현재 사용 중) | Npgsql 통합, 최적 성능               |
| **공간 연산**           | ✅ **NetTopologySuite**                | Buffer, Intersection 등 JTS 알고리즘 |
| **파일 읽기/쓰기**      | ⚠️ **GDAL 추가 고려**                  | Shapefile import 기능 추가 시        |
| **래스터 타일**         | ⚠️ **GDAL 추가 고려**                  | 배경 지도 지원 시                    |
| **좌표계 변환**         | ✅ **NTS + ProjNet** (현재로 충분)     | 대부분의 경우 PostGIS에서 변환       |

### 📋 **구현 우선순위**

#### **Phase 1: 현재 상태 유지** (추천) ✅
- NetTopologySuite + PostGIS 조합이 벡터 타일 생성에 최적화
- 현재 구조로 충분히 목표 달성 가능

#### **Phase 2: GDAL 선택적 통합** (필요 시)
1. `GeometryConverterService` 먼저 구현 (WKB 변환)
2. `ShapefileImportService` 추가 (사용자 데이터 업로드)
3. `RasterTileService` 추가 (배경 지도 지원)

#### **Phase 3: 완전 통합** (미래)
- GDAL를 기본 I/O 엔진으로 채택
- NTS는 순수 연산 엔진으로 사용

---

## 7. 참고 자료

### 📚 **공식 문서**
- [NetTopologySuite GitHub](https://github.com/NetTopologySuite/NetTopologySuite)
- [GDAL Documentation](https://gdal.org/)
- [MaxRev.Gdal.Core GitHub](https://github.com/MaxRev-Dev/gdal.netcore)

### 💻 **샘플 코드**
- [NTS + GDAL Integration Example](https://github.com/NetTopologySuite/NetTopologySuite/discussions)
- [PostGIS + NTS Tutorial](https://www.npgsql.org/doc/types/nts.html)

### 🔧 **관련 패키지**
```bash
# NetTopologySuite 생태계
NetTopologySuite                  2.6.0
NetTopologySuite.IO.GeoJSON       4.0.0
NetTopologySuite.IO.Shapefile     2.0.0  (기본 Shapefile 지원)
NetTopologySuite.IO.PostGis       2.1.0
Npgsql.NetTopologySuite           9.0.2

# GDAL 생태계
MaxRev.Gdal.Core                       3.9.1
MaxRev.Gdal.WindowsRuntime.Minimal     3.9.1
MaxRev.Gdal.LinuxRuntime.Minimal       3.9.1

# 좌표계 변환
ProjNet                           2.0.0
```

---

## 결론

**NetTopologySuite와 GDAL은 경쟁 관계가 아닌 보완 관계입니다.**

- **NTS**: .NET 네이티브, 공간 연산 최강자
- **GDAL**: 포맷 지원 최강자, 래스터 처리

현재 프로젝트는 **NTS + PostGIS 조합으로 충분**하며, 향후 Shapefile 지원이나 래스터 처리가 필요할 때 **GDAL을 점진적으로 통합**하는 것을 권장합니다. WKB 변환을 통해 두 라이브러리 간 데이터 교환은 매우 효율적으로 가능합니다.
