# SpatialTileBuilder

**SpatialTileBuilder** is a high-performance Windows desktop application built with **.NET 10** and **WPF**. It allows users to connect to a **PostGIS** database, select spatial tables, apply custom styles, and generate map tiles (XYZ/TMS) efficiently using parallel processing.

## Key Features

*   **Database Connectivity**: 
    *   Connect to PostgreSQL/PostGIS databases.
    *   Save and manage connection profiles.
    *   Automatic discovery of spatial tables and geometry columns.
*   **Layer Management**:
    *   View available schemas and tables.
    *   Select multiple layers for tile generation.
    *   Automatic coordinate system (SRID) detection and transformation logic.
*   **Advanced Styling**:
    *   **Fill Styling**: Customize fill color, opacity, and visibility.
    *   **Line Styling**: Adjust stroke color, width, and dash patterns (Solid, Dash, Dot).
    *   **Labeling**: Select attribute columns for labeling, customize font size, color, and halo.
    *   **Symbology**: Custom point markers and sizes.
    *   **Real-time Preview**: Visualize styling changes instantly on a map preview.
*   **Tile Generation**:
    *   Generate standard XYZ or TMS tiles.
    *   Multi-threaded high-performance rendering using **SkiaSharp**.
    *   Custom zoom level ranges and bounding box selection (Full, Sido, custom BBox).

## Technology Stack

*   **Framework**: .NET 10.0 (WPF)
*   **Architecture**: MVVM (CommunityToolkit.Mvvm)
*   **Database**: Npgsql (PostgreSQL), Dapper
*   **Geometry**: NetTopologySuite
*   **Rendering**: SkiaSharp (Hardware accelerated 2D graphics)
*   **UI**: MaterialDesignInXamlToolkit

## Getting Started

### Prerequisites
*   Windows 10/11 (x64)
*   .NET 10.0 Runtime or SDK

### Building the Project
1.  Clone the repository:
    ```bash
    git clone https://github.com/wgsystem-1/spatialtilebuilder.git
    ```
2.  Open the solution `SpatialTileBuilder.sln` in Visual Studio or Rider.
3.  Build contents in **Release** mode for `win-x64`.
    ```bash
    dotnet build -c Release -r win-x64
    ```

### Usage
1.  **Connection**: Enter your PostGIS DB credentials (Host, Port, User, Password, DB Name). You can save this connection for later.
2.  **Select Layers**: Check the spatial tables you want to render.
3.  **Style Editor**: Click on a layer to configure its colors, lines, and labels. Check the preview to verify.
4.  **Set Region**: Choose the zoom levels (e.g., 0-14) and the region (Korea, specific BBox, etc.).
5.  **Build Tiles**: Start the generation process. Tiles will be saved to your Documents folder by default.

## License
MIT License
