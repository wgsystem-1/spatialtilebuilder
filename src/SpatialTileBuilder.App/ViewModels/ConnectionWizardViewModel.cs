namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Entities;
using SpatialTileBuilder.Core.Interfaces;
using SpatialTileBuilder.App.Helpers;

public partial class ConnectionWizardViewModel : ObservableObject
{
    private readonly IPostGISConnectionService _connectionService;
    private readonly IConnectionRepository _repository;

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private string _port = "5432";

    [ObservableProperty]
    private string _database = string.Empty;

    [ObservableProperty]
    private string _username = "postgres";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _connectionName = string.Empty;

    [ObservableProperty]
    private string _sslMode = "Prefer";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _testSuccess;

    public ObservableCollection<string> SslModes { get; } = new() { "Disable", "Prefer", "Require" };

    public ObservableCollection<DbConnection> SavedConnections { get; } = new();

    private DbConnection? _selectedConnection;
    public DbConnection? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (SetProperty(ref _selectedConnection, value) && value != null)
            {
                // Autofill
                ConnectionName = value.Name;
                Host = value.Host;
                Port = value.Port.ToString();
                Database = value.Database;
                Username = value.Username;
                SslMode = value.SslMode;
                Password = SecurityHelper.DecryptString(value.PasswordEncrypted);
            }
        }
    }

    public ConnectionWizardViewModel(
        IPostGISConnectionService connectionService,
        IConnectionRepository repository)
    {
        _connectionService = connectionService;
        _repository = repository;
        
        _ = LoadSavedConnectionsAsync();
    }

    private async Task LoadSavedConnectionsAsync()
    {
        try 
        {
            var list = await _repository.GetAllAsync();
            SavedConnections.Clear();
            foreach(var item in list) SavedConnections.Add(item);
        }
        catch {}
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!ValidateInputs()) return;

        IsLoading = true;
        StatusMessage = "연결 테스트 중...";
        IsError = false;
        TestSuccess = false;

        try
        {
            var info = GetConnectionInfo();
            var result = await _connectionService.TestConnectionAsync(info);
            
            if (result)
            {
                StatusMessage = "연결 성공!";
                TestSuccess = true;
            }
            else
            {
                StatusMessage = "연결 실패. 설정을 확인해주세요.";
                IsError = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
            IsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateInputs()) return;

        // Auto-generate name if empty
        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            ConnectionName = $"{Host}_{Database}";
        }

        if (!TestSuccess)
        {
            await TestConnectionAsync();
            if (!TestSuccess) return;
        }

        IsLoading = true;
        try
        {
            var entity = new DbConnection
            {
                Name = ConnectionName,
                Host = Host,
                Port = int.Parse(Port),
                Database = Database,
                Username = Username,
                PasswordEncrypted = SecurityHelper.EncryptString(Password),
                SslMode = SslMode
            };

            if (await _repository.ExistsAsync(ConnectionName))
            {
                 // Find existing one to get ID
                 var all = await _repository.GetAllAsync();
                 var existing = all.FirstOrDefault(c => c.Name == ConnectionName);
                 if (existing != null)
                 {
                     entity.Id = existing.Id;
                     await _repository.UpdateAsync(entity);
                     StatusMessage = "기존 연결 정보를 업데이트했습니다.";
                 }
                 else
                 {
                     // Shoud not happen if ExistsAsync is true
                     await _repository.AddAsync(entity);
                     StatusMessage = "연결이 저장되었습니다.";
                 }
            }
            else
            {
                await _repository.AddAsync(entity);
                StatusMessage = "연결이 저장되었습니다.";
            }

            // Set the active connection for the application
            _connectionService.SetConnectionInfo(GetConnectionInfo());
            IsError = false;
            
            // Navigate logic is handled manually by user for now or auto via View
            // StatusMessage will prompt user they can proceed
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
            IsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(Host) || !int.TryParse(Port, out _))
        {
            StatusMessage = "올바른 호스트와 포트를 입력하세요.";
            IsError = true;
            return false;
        }
        if (string.IsNullOrWhiteSpace(Database))
        {
            StatusMessage = "데이터베이스 이름을 입력하세요.";
            IsError = true;
            return false;
        }
        if (string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "사용자명을 입력하세요.";
            IsError = true;
            return false;
        }
        
        IsError = false;
        StatusMessage = string.Empty;
        return true;
    }

    private ConnectionInfo GetConnectionInfo()
    {
        return new ConnectionInfo(
            Host,
            int.Parse(Port),
            Database,
            Username,
            Password,
            SslMode
        );
    }
}
