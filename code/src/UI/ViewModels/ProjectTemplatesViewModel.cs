﻿// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.Templates.Core;
using Microsoft.Templates.Core.Gen;
using Microsoft.Templates.Core.Mvvm;
using Microsoft.Templates.UI.Resources;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Microsoft.Templates.UI.ViewModels
{
    public class ProjectTemplatesViewModel : Observable
    {
        public MetadataInfoViewModel ContextFramework { get; set; }
        public MetadataInfoViewModel ContextProjectType { get; set; }

        private string _pagesHeader;
        public string PagesHeader
        {
            get => _pagesHeader;
            set => SetProperty(ref _pagesHeader, value);
        }

        private string _featuresHeader;
        public string FeaturesHeader
        {
            get => _featuresHeader;
            set => SetProperty(ref _featuresHeader, value);
        }

        public ObservableCollection<GroupTemplateInfoViewModel> PagesGroups { get; } = new ObservableCollection<GroupTemplateInfoViewModel>();
        public ObservableCollection<GroupTemplateInfoViewModel> FeatureGroups { get; } = new ObservableCollection<GroupTemplateInfoViewModel>();

        public ObservableCollection<SummaryItemViewModel> SummaryPages { get; } = new ObservableCollection<SummaryItemViewModel>();
        public ObservableCollection<SummaryItemViewModel> SummaryFeatures { get; } = new ObservableCollection<SummaryItemViewModel>();

        public List<TemplateSelection> SavedTemplates { get; } = new List<TemplateSelection>();

        public IEnumerable<TemplateSelection> SavedFeatures { get => SavedTemplates.Where(st => st.Template.GetTemplateType() == TemplateType.Feature); }
        public IEnumerable<TemplateSelection> SavedPages { get => SavedTemplates.Where(st => st.Template.GetTemplateType() == TemplateType.Page); }

        private RelayCommand<SummaryItemViewModel> _removeTemplateCommand;
        public RelayCommand<SummaryItemViewModel> RemoveTemplateCommand => _removeTemplateCommand ?? (_removeTemplateCommand = new RelayCommand<SummaryItemViewModel>(OnRemoveTemplate));

        private RelayCommand<TemplateInfoViewModel> _addTemplateCommand;
        public RelayCommand<TemplateInfoViewModel> AddTemplateCommand => _addTemplateCommand ?? (_addTemplateCommand = new RelayCommand<TemplateInfoViewModel>(OnAddTemplateItem));

        private RelayCommand<TemplateInfoViewModel> _saveTemplateCommand;
        public RelayCommand<TemplateInfoViewModel> SaveTemplateCommand => _saveTemplateCommand ?? (_saveTemplateCommand = new RelayCommand<TemplateInfoViewModel>(OnSaveTemplateItem));

        private RelayCommand<SummaryItemViewModel> _summaryItemOpenCommand;
        public RelayCommand<SummaryItemViewModel> SummaryItemOpenCommand => _summaryItemOpenCommand ?? (_summaryItemOpenCommand = new RelayCommand<SummaryItemViewModel>(OnSummaryItemOpen));

        private RelayCommand<SummaryItemViewModel> _summaryItemSetHomeCommand;
        public RelayCommand<SummaryItemViewModel> SummaryItemSetHomeCommand => _summaryItemSetHomeCommand ?? (_summaryItemSetHomeCommand = new RelayCommand<SummaryItemViewModel>(OnSummaryItemSetHome));

        private ICommand _renameSummaryItemCommand;
        public ICommand RenameSummaryItemCommand => _renameSummaryItemCommand ?? (_renameSummaryItemCommand = new RelayCommand<SummaryItemViewModel>(OnRenameSummaryItem));

        private ICommand _moveUpSummaryItemCommand;
        public ICommand MoveUpSummaryItemCommand => _moveUpSummaryItemCommand ?? (_moveUpSummaryItemCommand = new RelayCommand<SummaryItemViewModel>(OnMoveUpSummaryItem));

        private ICommand _moveDownSummaryItemCommand;
        public ICommand MoveDownSummaryItemCommand => _moveDownSummaryItemCommand ?? (_moveDownSummaryItemCommand = new RelayCommand<SummaryItemViewModel>(OnMoveDownSummaryItem));

        private ICommand _confirmRenameSummaryItemCommand;
        public ICommand ConfirmRenameSummaryItemCommand => _confirmRenameSummaryItemCommand ?? (_confirmRenameSummaryItemCommand = new RelayCommand<SummaryItemViewModel>(OnConfirmRenameSummaryItem));

        public ProjectTemplatesViewModel()
        {
            SummaryFeatures.CollectionChanged += (s, o) => { OnPropertyChanged(nameof(SummaryFeatures)); };
            SummaryPages.CollectionChanged += (s, o) => { OnPropertyChanged(nameof(SummaryPages)); };
        }

        private void ValidateCurrentTemplateName(SummaryItemViewModel item)
        {
            var names = SavedTemplates.Select(t => t.Name);
            var validationResult = Naming.Validate(names, item.NewItemName);

            item.IsValidName = validationResult.IsValid;
            item.ErrorMessage = String.Empty;

            if (!item.IsValidName)
            {
                item.ErrorMessage = StringRes.ResourceManager.GetString($"ValidationError_{validationResult.ErrorType}");

                if (string.IsNullOrWhiteSpace(item.ErrorMessage))
                {
                    item.ErrorMessage = "UndefinedError";
                }

                throw new Exception(item.ErrorMessage);
            }
        }

        private void ValidateNewTemplateName(TemplateInfoViewModel template)
        {
            var names = SavedTemplates.Select(t => t.Name);
            var validationResult = Naming.Validate(names, template.NewTemplateName);

            template.IsValidName = validationResult.IsValid;
            template.ErrorMessage = String.Empty;

            if (!template.IsValidName)
            {
                template.ErrorMessage = StringRes.ResourceManager.GetString($"ValidationError_{validationResult.ErrorType}");

                if (string.IsNullOrWhiteSpace(template.ErrorMessage))
                {
                    template.ErrorMessage = "UndefinedError";
                }

                throw new Exception(template.ErrorMessage);
            }
        }

        public async Task InitializeAsync()
        {
            MainViewModel.Current.Title = StringRes.ProjectTemplatesTitle;
            ContextProjectType = MainViewModel.Current.ProjectSetup.SelectedProjectType;
            ContextFramework = MainViewModel.Current.ProjectSetup.SelectedFramework;

            if (PagesGroups.Count == 0)
            {
                var pages = GenContext.ToolBox.Repo.Get(t => t.GetTemplateType() == TemplateType.Page && t.GetFrameworkList().Contains(ContextFramework.Name))
                                                   .Select(t => new TemplateInfoViewModel(t, GenComposer.GetAllDependencies(t, ContextFramework.Name), AddTemplateCommand, SaveTemplateCommand, ValidateNewTemplateName));

                var groups = pages.GroupBy(t => t.Group).Select(gr => new GroupTemplateInfoViewModel(gr.Key as string, gr.ToList())).OrderBy(gr => gr.Title);

                PagesGroups.AddRange(groups);
                PagesHeader = String.Format(StringRes.GroupPagesHeader_SF, pages.Count());
            }

            if (FeatureGroups.Count == 0)
            {
                var features = GenContext.ToolBox.Repo.Get(t => t.GetTemplateType() == TemplateType.Feature && t.GetFrameworkList().Contains(ContextFramework.Name))
                                                      .Select(t => new TemplateInfoViewModel(t, GenComposer.GetAllDependencies(t, ContextFramework.Name), AddTemplateCommand, SaveTemplateCommand, ValidateNewTemplateName));

                var groups = features.GroupBy(t => t.Group).Select(gr => new GroupTemplateInfoViewModel(gr.Key as string, gr.ToList())).OrderBy(gr => gr.Title);

                FeatureGroups.AddRange(groups);
                FeaturesHeader = String.Format(StringRes.GroupFeaturesHeader_SF, features.Count());
            }

            if (SavedTemplates == null || SavedTemplates.Count == 0)
            {
                SetupTemplatesFromLayout(ContextProjectType.Name, ContextFramework.Name);
                MainViewModel.Current.RebuildLicenses();
            }
            MainViewModel.Current.SetTemplatesReadyForProjectCreation();
            CloseTemplatesEdition();
            await Task.CompletedTask;
        }

        public void ResetSelection()
        {
            SummaryPages.Clear();
            SummaryFeatures.Clear();
            SavedTemplates.Clear();
            PagesGroups.Clear();
            FeatureGroups.Clear();
        }

        private void OnAddTemplateItem(TemplateInfoViewModel template)
        {
            if (template.CanChooseItemName)
            {
                var names = SavedTemplates.Select(t => t.Name);
                template.NewTemplateName = Naming.Infer(names, template.Template.GetDefaultName());
                CloseTemplatesEdition();
                template.IsEditionEnabled = true;
            }
            else
            {
                template.NewTemplateName = template.Template.GetDefaultName();
                var templateSelection = new TemplateSelection()
                {
                    Name = template.NewTemplateName,
                    Template = template.Template
                };
                SetupTemplateAndDependencies(templateSelection);
                var isAlreadyDefined = IsTemplateAlreadyDefined(template.Template.Identity);
                template.UpdateTemplateAvailability(isAlreadyDefined);
            }
        }

        private void OnSaveTemplateItem(TemplateInfoViewModel template)
        {
            if (template.IsValidName)
            {
                var templateSelection = new TemplateSelection()
                {
                    Name = template.NewTemplateName,
                    Template = template.Template
                };
                SetupTemplateAndDependencies(templateSelection);
                template.CloseEdition();

                var isAlreadyDefined = IsTemplateAlreadyDefined(template.Template.Identity);
                template.UpdateTemplateAvailability(isAlreadyDefined);
            }
        }

        private void OnSummaryItemOpen(SummaryItemViewModel item)
        {
            if (item.IsOpen)
            {
                item.IsOpen = false;
            }
            else
            {
                foreach (var page in SummaryPages) { page.TryClose(); }

                foreach (var feature in SummaryFeatures) { feature.TryClose(); }

                item.IsOpen = true;
            }
        }

        private void OnRenameSummaryItem(SummaryItemViewModel item)
        {
            CloseSummaryItemsEdition();
            item.IsEditionEnabled = true;
            item.TryClose();
        }

        private void OnConfirmRenameSummaryItem(SummaryItemViewModel item)
        {
            var names = SavedTemplates.Select(t => t.Name);
            var validationResult = Naming.Validate(names, item.NewItemName);

            if (validationResult.IsValid)
            {
                var savedTemplate = SavedTemplates.First(st => st.Name == item.ItemName);
                savedTemplate.Name = item.NewItemName;
                item.ItemName = item.NewItemName;
                item.IsEditionEnabled = false;
            }
        }

        private void OnMoveDownSummaryItem(SummaryItemViewModel item)
        {
            var oldIndex = SummaryPages.IndexOf(item);
            if (oldIndex < SummaryPages.Count - 1)
            {
                int newIndex = oldIndex + 1;
                SummaryPages.RemoveAt(oldIndex);
                SummaryPages.Insert(newIndex, item);

                var savedTemplate = SavedTemplates.First(t => t.Name == item.ItemName);
                int oldIndexST = SavedTemplates.IndexOf(savedTemplate);
                int newIndexST = oldIndexST + 1;
                SavedTemplates.RemoveAt(oldIndexST);
                SavedTemplates.Insert(newIndexST, savedTemplate);
            }
            UpdateCanMoveUpAndDownPages();
        }

        private void OnMoveUpSummaryItem(SummaryItemViewModel item)
        {
            int oldIndex = SummaryPages.IndexOf(item);
            if (oldIndex > 0)
            {
                int newIndex = oldIndex - 1;
                SummaryPages.RemoveAt(oldIndex);
                SummaryPages.Insert(newIndex, item);

                var savedTemplate = SavedTemplates.First(t => t.Name == item.ItemName);
                int oldIndexST = SavedTemplates.IndexOf(savedTemplate);
                int newIndexST = oldIndexST - 1;
                SavedTemplates.RemoveAt(oldIndexST);
                SavedTemplates.Insert(newIndexST, savedTemplate);
            }
            UpdateCanMoveUpAndDownPages();
        }

        private void UpdateCanMoveUpAndDownPages()
        {
            int index = 0;
            foreach (var page in SummaryPages)
            {
                page.CanMoveUp = index > 1;
                page.CanMoveDown = index > 0 && index < SummaryPages.Count - 1;
                index++;
            }
        }

        private void OnSummaryItemSetHome(SummaryItemViewModel item)
        {
            if (!item.IsHome)
            {
                foreach (var page in SummaryPages) { page.TryReleaseHome(); }
                foreach (var page in SavedTemplates) { page.IsHome = false; }

                item.IsHome = true;

                var savedTemplate = SavedTemplates.First(st => st.Name == item.ItemName);
                savedTemplate.IsHome = true;

                int oldIndex = SummaryPages.IndexOf(item);
                if (oldIndex > 0)
                {
                    SummaryPages.RemoveAt(oldIndex);
                    SummaryPages.Insert(0, item);

                    int oldIndexST = SavedTemplates.IndexOf(savedTemplate);
                    SavedTemplates.RemoveAt(oldIndexST);
                    SavedTemplates.Insert(0, savedTemplate);
                }
                UpdateCanMoveUpAndDownPages();                
            }
        }

        private void OnRemoveTemplate(SummaryItemViewModel item)
        {
            if (SavedTemplates.Any(st => st.Template.GetDependencyList().Any(d => d == item.Identity)))
            {
                var dependencyName = SavedTemplates.First(st => st.Template.GetDependencyList().Any(d => d == item.Identity));
                string message = String.Format(StringRes.ValidationError_CanNotRemoveTemplate_SF, item.TemplateName, dependencyName.Template.Name, dependencyName.Template.GetTemplateType());

                MainViewModel.Current.Status = new StatusViewModel(Controls.StatusType.Warning, message, true);

                return;
            }
            if (SummaryPages.Contains(item))
            {
                SummaryPages.Remove(item);
            }
            else if (SummaryFeatures.Contains(item))
            {
                SummaryFeatures.Remove(item);
            }

            SavedTemplates.Remove(SavedTemplates.First(st => st.Name == item.ItemName));
            MainViewModel.Current.CreateCommand.OnCanExecuteChanged();
            UpdateTemplatesAvailability();
            MainViewModel.Current.RebuildLicenses();
        }

        private bool IsTemplateAlreadyDefined(string identity)
        {
            return SavedTemplates.Select(t => t.Template.Identity).Any(name => name == identity);
        }

        private void CloseTemplatesEdition()
        {
            PagesGroups.ToList().ForEach(g => g.Templates.ToList().ForEach(t => t.CloseEdition()));
            FeatureGroups.ToList().ForEach(g => g.Templates.ToList().ForEach(t => t.CloseEdition()));
        }

        private void CloseSummaryItemsEdition()
        {
            SummaryPages.ToList().ForEach(p => p.CloseEdition());
            SummaryFeatures.ToList().ForEach(f => f.CloseEdition());
        }

        private void UpdateTemplatesAvailability()
        {
            PagesGroups.ToList().ForEach(g => g.Templates.ToList().ForEach(t =>
            {
                var isAlreadyDefined = IsTemplateAlreadyDefined(t.Template.Identity);
                t.UpdateTemplateAvailability(isAlreadyDefined);
            }));

            FeatureGroups.ToList().ForEach(g => g.Templates.ToList().ForEach(t =>
            {
                var isAlreadyDefined = IsTemplateAlreadyDefined(t.Template.Identity);
                t.UpdateTemplateAvailability(isAlreadyDefined);
            }));
        }

        private void SetupTemplatesFromLayout(string projectTypeName, string frameworkName)
        {
            var layout = GenComposer.GetLayoutTemplates(projectTypeName, frameworkName);

            foreach (var item in layout)
            {
                if (item.Template != null)
                {
                    var templateSelection = new TemplateSelection()
                    {
                        Name = item.Layout.name,
                        Template = item.Template
                    };
                    SetupTemplateAndDependencies(templateSelection, !item.Layout.@readonly);
                }
            }
        }

        private void SetupTemplateAndDependencies(TemplateSelection item, bool isRemoveEnabled = true)
        {
            SaveNewTemplate(item, isRemoveEnabled);
            var dependencies = GenComposer.GetAllDependencies(item.Template, ContextFramework.Name);

            foreach (var dependencyTemplate in dependencies)
            {
                if (!SavedTemplates.Any(s => s.Template.Identity == dependencyTemplate.Identity))
                {
                    var templateSelection = new TemplateSelection()
                    {
                        Name = dependencyTemplate.GetDefaultName(),
                        Template = dependencyTemplate
                    };
                    SaveNewTemplate(templateSelection, isRemoveEnabled);
                }
            }

            MainViewModel.Current.RebuildLicenses();
        }

        private void SaveNewTemplate(TemplateSelection item, bool isRemoveEnabled = true)
        {
            SavedTemplates.Add(item);

            var newItem = new SummaryItemViewModel(item, isRemoveEnabled, RemoveTemplateCommand, SummaryItemOpenCommand, SummaryItemSetHomeCommand, RenameSummaryItemCommand, ConfirmRenameSummaryItemCommand, MoveUpSummaryItemCommand, MoveDownSummaryItemCommand, ValidateCurrentTemplateName);

            if (item.Template.GetTemplateType() == TemplateType.Page)
            {
                if (SummaryPages.Count == 0)
                {
                    newItem.IsHome = true;
                }
                SummaryPages.Add(newItem);
            }
            else if (item.Template.GetTemplateType() == TemplateType.Feature)
            {
                SummaryFeatures.Add(newItem);
            }
            UpdateTemplatesAvailability();
            UpdateCanMoveUpAndDownPages();
        }
    }
}
