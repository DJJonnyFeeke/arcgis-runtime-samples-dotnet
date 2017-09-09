﻿// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UIKit;

namespace ArcGISRuntimeXamarin.Samples.StatsQueryGroupAndSort
{
    [Register("StatsQueryGroupAndSort")]
    public class StatsQueryGroupAndSort : UIViewController
    {
        // URI for the US states map service
        private Uri _usStatesServiceUri = new Uri("https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer/3");

        // US states feature table
        private FeatureTable _usStatesTable;

        // List of field names from the table
        private List<string> _fieldNames;

        // Selected fields for grouping results
        private Dictionary<string, bool> _groupByFields;

        // Collection to hold fields to order results by
        private List<OrderFieldOption> _orderByFields = new List<OrderFieldOption>();

        // Stack view UI control for arranging query controls
        private UIStackView _controlsStackView;

        // Model for defining choices in the statistics definition UIPickerView
        private StatDefinitionModel _statsPickerModel;

        // List of statistics definitions to use in the query
        private List<StatisticDefinition> _statisticDefinitions;

        public StatsQueryGroupAndSort()
        {
            Title = "Stats query with grouped results";
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Create the UI
            CreateLayout();

            // Initialize the map and layers
            Initialize();
        }

        public override void ViewDidLayoutSubviews()
        {
            // Get height of status bar and navigation bar
            nfloat pageOffset = NavigationController.NavigationBar.Frame.Size.Height + UIApplication.SharedApplication.StatusBarFrame.Height;

            // Setup the visual frame for the query controls
            _controlsStackView.Frame = new CoreGraphics.CGRect(0,  pageOffset, View.Bounds.Width, View.Bounds.Height - pageOffset);

            base.ViewDidLayoutSubviews();
        }

        private async void Initialize()
        {
            // Create the US states feature table
            _usStatesTable = new ServiceFeatureTable(_usStatesServiceUri);

            // Load the table
            await _usStatesTable.LoadAsync();

            // Fill the fields combo and "group by" list with field names from the table
            _fieldNames = _usStatesTable.Fields.Select(f => f.Name).ToList();

            // Create a model that will provide statistic definition choices for the picker
            _statsPickerModel = new StatDefinitionModel(_fieldNames.ToArray());

            // Create a list of fields the user can select for grouping
            _groupByFields = new Dictionary<string, bool>();
            foreach(var name in _fieldNames)
            {
                _groupByFields.Add(name, false);
            }
        }

        private void CreateLayout()
        {
            this.View.BackgroundColor = UIColor.White;

            // Create a stack view to organize the query controls
            _controlsStackView = new UIStackView();
            _controlsStackView.Axis = UILayoutConstraintAxis.Vertical;
            _controlsStackView.Alignment = UIStackViewAlignment.Center;
            _controlsStackView.Distribution = UIStackViewDistribution.EqualSpacing;
            _controlsStackView.Spacing = 5;

            // Button for launching the UI to view or define statistics definitions for the query
            var showStatDefinitionsButton = new UIButton();
            showStatDefinitionsButton.SetTitle("Statistic Definitions", UIControlState.Normal);
            showStatDefinitionsButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            showStatDefinitionsButton.BackgroundColor = UIColor.White;
            showStatDefinitionsButton.Frame = new CoreGraphics.CGRect(30, 20, 220, 30);
            showStatDefinitionsButton.TouchUpInside += ShowStatDefinitions;

            // Button to choose fields with which to group results
            var showGroupFieldsButton = new UIButton();
            showGroupFieldsButton.SetTitle("Group Fields", UIControlState.Normal);
            showGroupFieldsButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            showGroupFieldsButton.BackgroundColor = UIColor.White;
            showGroupFieldsButton.Frame = new CoreGraphics.CGRect(30, 60, 220, 30);
            showGroupFieldsButton.TouchUpInside += ShowGroupFields;

            // Create a button to invoke the query
            var getStatsButton = new UIButton();
            getStatsButton.SetTitle("Get Statistics", UIControlState.Normal);
            getStatsButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            getStatsButton.BackgroundColor = UIColor.White;

            // Handle the button tap to execute the statistics query
            getStatsButton.TouchUpInside += OnExecuteStatisticsQueryClicked;

            // Add controls to the stack view
            // TODO: add other UI controls
            _controlsStackView.AddSubviews(showStatDefinitionsButton, showGroupFieldsButton);

            //_controlsStackView.AddArrangedSubview(getStatsButton);

            // Add UI controls to the page
            View.AddSubview(_controlsStackView);
        }

        private void ShowGroupFields(object sender, EventArgs e)
        {
            // Create a new table 
            var fieldsTable = new UITableViewController(UITableViewStyle.Plain);

            // Create a data source to show fields the user can choose to group results with
            var groupFieldsDataSource = new GroupFieldsDataSource(_groupByFields);

            // Set the data source on the table
            fieldsTable.TableView.Source = groupFieldsDataSource;

            // Show the table view
            this.NavigationController.PushViewController(fieldsTable, true);
        }

        private void ShowStatDefinitions(object sender, EventArgs e)
        {
            // Create a list to store statistic definitions (if it doesn't exist)
            if (_statisticDefinitions == null)
            {
                _statisticDefinitions = new List<StatisticDefinition>();
            }

            // Create a new UIPickerView and assign a model that will show fields and statistic types
            var statisticPicker = new UIPickerView();
            statisticPicker.Model = _statsPickerModel;

            // Create a new table 
            var statsTable = new UITableViewController(UITableViewStyle.Plain);

            // Create a data source to show statistic definitions in the table
            // Pass in the list of statistic definitions and the picker (for defining new ones)
            var statDefsDataSource = new StatisticDefinitionsDataSource(_statisticDefinitions, statisticPicker);
            
            // Set the data source on the table
            statsTable.TableView.Source = statDefsDataSource;

            // Put the table in edit mode (to show add and delete buttons)
            statDefsDataSource.WillBeginTableEditing(statsTable.TableView);
            statsTable.SetEditing(true, true);

            // Show the table view
            this.NavigationController.PushViewController(statsTable, true);
        }

        private async void OnExecuteStatisticsQueryClicked(object sender, EventArgs e)
        {
            // Create definitions for each statistic to calculate

            // Create a definition for count that includes an alias for the output
            StatisticDefinition statDefinitionCount = new StatisticDefinition("POP", StatisticType.Count, "CityCount");

            // Add the statistics definitions to a list
            List<StatisticDefinition> statDefinitions = new List<StatisticDefinition>
                        {
                        statDefinitionCount
                        };

            // Create the statistics query parameters, pass in the list of definitions
            StatisticsQueryParameters statQueryParams = new StatisticsQueryParameters(statDefinitions);


            // Execute the statistical query with these parameters and await the results
            StatisticsQueryResult statQueryResult = await _usStatesTable.QueryStatisticsAsync(statQueryParams);

            // Get the first (only) StatisticRecord in the results
            StatisticRecord record = statQueryResult.FirstOrDefault();

            // Make sure a record was returned
            if (record == null || record.Statistics.Count == 0)
            {
                // Notify the user that no results were returned
                UIAlertView alert = new UIAlertView();
                alert.Message = "No results were returned";
                alert.Title = "Statistical Query";
                alert.Show();
                return;
            }

            // Display results
            IReadOnlyDictionary<string, object> statistics = record.Statistics;
            ShowStatsList(statistics);
        }

        private void ShowStatsList(IReadOnlyDictionary<string, object> stats)
        {
            // Create a new Alert Controller
            UIAlertController statsAlert = UIAlertController.Create("Statistics", string.Empty, UIAlertControllerStyle.Alert);

            // Loop through all key/value pairs in the results
            foreach (KeyValuePair<string, object> kvp in stats)
            {
                // If the value is null, display "--"
                string displayString = "--";

                if (kvp.Value != null)
                {
                    displayString = kvp.Value.ToString();
                }

                // Add the statistics info as an alert action
                statsAlert.AddAction(UIAlertAction.Create(kvp.Key + " : " + displayString, UIAlertActionStyle.Default, null));
            }

            // Add an Action to dismiss the alert
            statsAlert.AddAction(UIAlertAction.Create("Dismiss", UIAlertActionStyle.Cancel, null));

            // Display the alert
            PresentViewController(statsAlert, true, null);
        }
    }

    // Simple class to describe an "order by" option
    public class OrderFieldOption
    {
        // Whether or not to use this field to order results
        public bool OrderWith { get; set; }

        // The order by info: field name and sort order
        public OrderBy OrderInfo { get; set; }

        public OrderFieldOption(bool orderWith, OrderBy orderInfo)
        {
            OrderWith = orderWith;
            OrderInfo = orderInfo;
        }
    }

    public class StatDefinitionModel : UIPickerViewModel
    {
        private string[] _fieldNames;
        private Array _statTypes = Enum.GetValues(typeof(StatisticType));
        private StatisticDefinition _selectedStatDefinition = null;

        public StatisticDefinition SelectedStatDefinition
        {
            get { return _selectedStatDefinition; }
        }

        public override nint GetComponentCount(UIPickerView pickerView)
        {
            return 2;
        }

        public StatDefinitionModel(string[] fieldNames)
        {
            this._fieldNames = fieldNames;
        }

        public override nint GetRowsInComponent(UIPickerView pickerView, nint component)
        {
            if (component == 0)
                return _fieldNames.Length;
            else
                return _statTypes.Length;
        }

        public override string GetTitle(UIPickerView pickerView, nint row, nint component)
        {
            if (component == 0)
                return _fieldNames[row];
            else
                return _statTypes.GetValue(row).ToString();
        }

        public override void Selected(UIPickerView pickerView, nint row, nint component)
        {
            var onFieldName = _fieldNames[pickerView.SelectedRowInComponent(0)];
            var statType = (StatisticType)_statTypes.GetValue(pickerView.SelectedRowInComponent(1));
            var outAlias = onFieldName + "_" + statType.ToString();
            _selectedStatDefinition = new StatisticDefinition(onFieldName, statType, outAlias);
        }

        public override nfloat GetComponentWidth(UIPickerView picker, nint component)
        {
            if (component == 0)
                return 160f;
            else
                return 120f;
        }

        public override nfloat GetRowHeight(UIPickerView picker, nint component)
        {
            return 40f;
        }
    }
    
    public class StatisticDefinitionsDataSource : UITableViewSource
    {
        // List of statistic definitions for the current query
        private List<StatisticDefinition> _statisticDefinitions;

        // Picker for choosing a field and statistic type
        private UIPickerView _statPicker;

        // Custom UI to show the statistics picker and associated buttons
        private ChooseStatisticOverlay _chooseStatOverlay;
        
        // Text to display for the placeholder row used to add new statistic definitions
        private const string  AddNewStatFieldName = "(Add statistic)";

        public StatisticDefinitionsDataSource(List<StatisticDefinition> statDefs, UIPickerView picker)
        {
            // Store the list of statistic definitions and the statistic picker
            _statisticDefinitions = statDefs;
            _statPicker = picker;
        }

        public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            // Respond to the user's edit request: Insert a new statistic definition, or delete an existing one
            if(editingStyle == UITableViewCellEditingStyle.Insert)
            {
                // Get the bounds of the table
                var ovBounds = tableView.Bounds;
                ovBounds.Height = ovBounds.Height + 60;

                // Create an overlay UI that lets the user choose a field and statistic type to add
                _chooseStatOverlay = new ChooseStatisticOverlay(ovBounds, 0.90f, UIColor.White, _statPicker);

                // Handle the OnStatisticDefined event to get the info entered by the user
                _chooseStatOverlay.OnStatisticDefined += (s,statDef) => 
                {
                    // Verify the selected statistic doesn't exist in the collection (check for an alias with the same value)
                    StatisticDefinition existingItem = _statisticDefinitions.Find(itm => itm.OutputAlias == statDef.OutputAlias);
                    if (existingItem != null) { return; }

                    // Make updates to the table (add the chosen statistic)
                    tableView.BeginUpdates();
                                        
                    // Insert a new row at the top of table display
                    tableView.InsertRows(new NSIndexPath[] { NSIndexPath.FromRowSection(0, 0)}, UITableViewRowAnimation.Fade);
                    
                    // Insert the chosen statistic in the underlying collection
                    _statisticDefinitions.Insert(0, statDef);

                    // Apply table edits
                    tableView.EndUpdates();
                };

                // Handle when the user chooses to close the dialog 
                _chooseStatOverlay.OnCanceled += (s,e)=> 
                { 
                    // Remove the item input UI
                    _chooseStatOverlay.Hide();
                    _chooseStatOverlay = null;
                };

                // Add the map item info UI view (will display semi-transparent over the map view)
                tableView.Add(_chooseStatOverlay);
            }
            else if(editingStyle == UITableViewCellEditingStyle.Delete)
            {
                // Remove the selected row from the table and the underlying collection of statistic definitions
                _statisticDefinitions.RemoveAt(indexPath.Row);
                tableView.DeleteRows(new NSIndexPath[] { indexPath }, UITableViewRowAnimation.Fade);
            }
        }

        // Define the (confirmation) text to display when the user chooses to delete a row 
        public override string TitleForDeleteConfirmation(UITableView tableView, NSIndexPath indexPath)
        {
            return "Remove";
        }

        // Allow all rows to be edited
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
        {
            return true;
        }

        // Allow all rows to be deleted except the last row, which is a placeholder for creating new statistic definitions
        public override UITableViewCellEditingStyle EditingStyleForRow(UITableView tableView, NSIndexPath indexPath)
        {
            // Get the index of the last row in the table view
            var lastRowIndex = tableView.NumberOfRowsInSection(0) - 1;

            // Set the editing style as delete for all but the final row (insert)
            if (indexPath.Row == lastRowIndex)
            {
                return UITableViewCellEditingStyle.Insert;
            }
            else
            {
                return UITableViewCellEditingStyle.Delete;
            }
        }

        public void WillBeginTableEditing(UITableView tableView)
        {
            // See if the table already has a placeholder row for the "Add New" button
            StatisticDefinition existingItem = _statisticDefinitions.Find((itm) => itm.OnFieldName == AddNewStatFieldName);

            // Return if there is already a placeholder row
            if(existingItem != null) { return; }

            // Begin updating the table
            tableView.BeginUpdates();

            // Create an index path for the last row in the table
            var lastRowIndex = NSIndexPath.FromRowSection(tableView.NumberOfRowsInSection(0), 0);

            // Add the insert placeholder row at the end of table display
            tableView.InsertRows(new NSIndexPath[] { lastRowIndex }, UITableViewRowAnimation.Fade);

            // Create a new StatisticDefinition and add it to the underlying data
            _statisticDefinitions.Add(new StatisticDefinition(AddNewStatFieldName, StatisticType.Count, ""));

            // Apply the table edits
            tableView.EndUpdates();
        }

        // This is called each time a cell needs to be created in the table
        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            // Create a new cell with a main and detail label style
            var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);

            // Get the corresponding StatisticDefinition for this row
            var definition = _statisticDefinitions[indexPath.Row] as StatisticDefinition;

            // Set the cell text with the field name
            cell.TextLabel.Text = definition.OnFieldName;

            // If this is not the placeholder (insert) row, set the detail text with the statistic type
            if (definition.OnFieldName != AddNewStatFieldName)
            {
                cell.DetailTextLabel.Text = definition.StatisticType.ToString();
            }

            // Return the new cell
            return cell;
        }

        // Return the number of rows for the table (count of the statistics definition list)
        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return _statisticDefinitions.Count;
        }
    }

    public class GroupFieldsDataSource : UITableViewSource
    {
        private Dictionary<string, bool> _potentialGroupFields;        

        public GroupFieldsDataSource(Dictionary<string, bool> fields)
        {
            _potentialGroupFields = fields;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = new UITableViewCell(UITableViewCellStyle.Default, null);

            string fieldName = _potentialGroupFields.ElementAt(indexPath.Row).Key;
            bool isForGrouping = _potentialGroupFields.ElementAt(indexPath.Row).Value;

            cell.TextLabel.Text = fieldName;

            // Create a UISwitch for selecting the field for grouping
            var groupFieldSwitch = new UISwitch()
            {
                Frame = new CoreGraphics.CGRect(cell.Bounds.Width - 60, 7, 50, cell.Bounds.Height)
            };
            groupFieldSwitch.Tag = indexPath.Row;
            groupFieldSwitch.On = isForGrouping;
            groupFieldSwitch.ValueChanged += GroupBySwitched;

            // Add the UISwitch to the cell's content view
            cell.ContentView.AddSubview(groupFieldSwitch);

            return cell;
        }

        private void GroupBySwitched(object sender, EventArgs e)
        {
            // Get the row containing the UISwitch that was changed
            var index = (sender as UISwitch).Tag;

            // Set the sublayer visibility according to the UISwitch setting
            var key = _potentialGroupFields.ElementAt((int)index).Key;
            _potentialGroupFields[key] = (sender as UISwitch).On;
        }

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return _potentialGroupFields.Count;
        }
    }

    // View containing "define statistic" controls (picker for fields/stat type, add/cancel buttons)
    public class ChooseStatisticOverlay : UIView
    {
        // Event to provide the statistic definition the user entered when the view closes
        public event EventHandler<StatisticDefinition> OnStatisticDefined;

        // Event to report that the choice was canceled
        public event EventHandler OnCanceled;

        // Store the input controls so the values can be read
        private UIPickerView _statisticPicker;
        
        public ChooseStatisticOverlay(CoreGraphics.CGRect frame, nfloat transparency, UIColor color, UIPickerView statPicker) : base(frame)
        {
            // Store the statistics picker
            _statisticPicker = statPicker;
            
            // Create a semi-transparent overlay with the specified background color
            BackgroundColor = color;
            Alpha = transparency;

            // Set the total height and width of the control set
            nfloat totalHeight = 400;
            nfloat totalWidth = 320;

            // Find the bottom x and y of the view
            nfloat centerX = Frame.Width / 2;
            nfloat centerY = Frame.Bottom - 40;

            // Find the start x and y for the control layout (aligned to the bottom of the view)
            nfloat controlX = centerX - (totalWidth / 2);
            nfloat controlY = centerY - totalHeight;

            // Toolbar with "Add" and "Done" buttons
            UIToolbar toolbar = new UIToolbar();
            toolbar.BarStyle = UIBarStyle.Black;
            toolbar.Translucent = false;
            toolbar.SizeToFit();

            // Add Button (add the new stat and don't dismiss the UI)
            UIBarButtonItem addButton = new UIBarButtonItem("Add", UIBarButtonItemStyle.Done, (s, e) =>
            {
                // Get the selected StatisticDefinition
                var statPickerModel = _statisticPicker.Model as StatDefinitionModel;
                var newStatDefinition = statPickerModel.SelectedStatDefinition;
                if (newStatDefinition != null)
                {
                    // Fire the OnMapInfoEntered event and provide the statistic definition
                    if (OnStatisticDefined != null)
                    {
                        // Raise the event
                        OnStatisticDefined(this, newStatDefinition);
                    }
                }
            });

            // Done Button (dismiss the UI, don't use the selected statistic)
            UIBarButtonItem doneButton = new UIBarButtonItem("Done", UIBarButtonItemStyle.Plain, (s, e) =>
            {
                OnCanceled.Invoke(this, null);
            });

            toolbar.SetItems(new UIBarButtonItem[] { addButton, doneButton }, true);

            // Define the location of the statistic picker
            controlY = controlY + 200;
            _statisticPicker.Frame = new CoreGraphics.CGRect(controlX, controlY, totalWidth, 200);

            // Set the location for the toolbar
            controlY = controlY + 220;
            toolbar.Frame = new CoreGraphics.CGRect(controlX, controlY, totalWidth, 30);

            // Add the controls
            AddSubviews(toolbar, _statisticPicker);
        }

        // Animate increasing transparency to completely hide the view, then remove it
        public void Hide()
        {
            // Action to make the view transparent
            Action makeTransparentAction = () => Alpha = 0;

            // Action to remove the view
            Action removeViewAction = () => RemoveFromSuperview();

            // Time to complete the animation (seconds)
            double secondsToComplete = 0.75;

            // Animate transparency to zero, then remove the view
            Animate(secondsToComplete, makeTransparentAction, removeViewAction);
        }
    }
}