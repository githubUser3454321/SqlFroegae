using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class CustomerMappingAdminViewModel : ObservableObject
{
    private readonly ICustomerMappingRepository _mappingRepository;

    public ObservableCollection<CustomerMappingItem> Mappings { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private CustomerMappingItem? _selected;

    [ObservableProperty] private string _customerCode = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _databaseUser = "om";
    [ObservableProperty] private string _objectPrefix = "om_";

    public CustomerMappingAdminViewModel()
    {
        _mappingRepository = App.Services.GetRequiredService<ICustomerMappingRepository>();
    }

    partial void OnSelectedChanged(CustomerMappingItem? value)
    {
        if (value is null)
            return;

        CustomerCode = value.CustomerCode;
        CustomerName = value.CustomerName;
        DatabaseUser = value.DatabaseUser;
        ObjectPrefix = value.ObjectPrefix;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var rows = await _mappingRepository.GetAllAsync();
            Mappings.Clear();
            foreach (var row in rows)
                Mappings.Add(row);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewMapping()
    {
        Selected = null;
        CustomerCode = "";
        CustomerName = "";
        DatabaseUser = "om";
        ObjectPrefix = "om_";
        Error = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            if (string.IsNullOrWhiteSpace(CustomerCode) || string.IsNullOrWhiteSpace(CustomerName))
                throw new InvalidOperationException("Customer code and name are required.");

            var item = new CustomerMappingItem
            {
                CustomerId = Selected?.CustomerId ?? Guid.NewGuid(),
                CustomerCode = CustomerCode.Trim(),
                CustomerName = CustomerName.Trim(),
                DatabaseUser = string.IsNullOrWhiteSpace(DatabaseUser) ? "om" : DatabaseUser.Trim(),
                ObjectPrefix = string.IsNullOrWhiteSpace(ObjectPrefix) ? "om_" : ObjectPrefix.Trim()
            };

            await _mappingRepository.UpsertAsync(item);
            await LoadAsync();
            Selected = item;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is null)
            return;

        try
        {
            IsBusy = true;
            Error = null;

            await _mappingRepository.DeleteAsync(Selected.CustomerId);
            NewMapping();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
