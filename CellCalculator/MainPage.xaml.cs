using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace CellCalculator
{
    public partial class MainPage : ContentPage
    {
        private SpreadsheetModel model;
        private int rows = 10, cols = 10;
        private readonly Dictionary<string, Entry> cellEntries = new Dictionary<string, Entry>();

        public MainPage()
        {
            InitializeComponent();

            model = new SpreadsheetModel(rows, cols);

            ModePicker.SelectedIndex = 1;
            RowsEntry.Text = rows.ToString();
            ColsEntry.Text = cols.ToString();

            ModePicker.SelectedIndexChanged += OnModePickerChanged;

            BuildGrid();
            RenderCells();
        }

        void BuildGrid()
        {
            TableGrid.RowDefinitions.Clear();
            TableGrid.ColumnDefinitions.Clear();
            TableGrid.Children.Clear();
            cellEntries.Clear();

            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int c = 1; c <= cols; c++)
            {
                TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TableGrid.Add(new Label { Text = "", FontAttributes = FontAttributes.Bold, TextColor = Colors.Black }, 0, 0);

            for (int c = 1; c <= cols; c++)
            {
                var hdr = SpreadsheetModel.ToColName(c);
                TableGrid.Add(new Label { Text = hdr, FontAttributes = FontAttributes.Bold, Padding = 4, TextColor = Colors.Black }, c, 0);
            }

            for (int r = 1; r <= rows; r++)
            {
                TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                TableGrid.Add(new Label { Text = r.ToString(), FontAttributes = FontAttributes.Bold, Padding = 4, TextColor = Colors.Black }, 0, r);

                for (int c = 1; c <= cols; c++)
                {
                    var addr = SpreadsheetModel.ToAddr(c, r);

                    if (!model.Cells.TryGetValue(addr, out var cell))
                        continue;

                    var entry = new Entry { Text = cell.Expression ?? "", WidthRequest = 120, TextColor = Colors.Black };
                    entry.Completed += OnCellEditCompleted;
                    entry.Unfocused += OnCellEditCompleted;
                    entry.Focused += OnEntryFocused;
                    entry.BindingContext = addr;

                    TableGrid.Add(entry, c, r);
                    cellEntries[addr] = entry;
                }
            }
        }

        void RenderCells()
        {
            bool showExpression = ModePicker.SelectedIndex == 0;

            foreach (var kv in model.Cells)
            {
                var addr = kv.Key;
                var cell = kv.Value;

                if (!cellEntries.ContainsKey(addr))
                    continue;

                var entry = cellEntries[addr];
                entry.TextColor = Colors.Black;

                if (showExpression)
                {
                    entry.Text = cell.Expression ?? "";
                    entry.IsReadOnly = false;
                    entry.BackgroundColor = cell.HasError ? Colors.LightPink : Colors.White;
                }
                else
                {
                    entry.Text = cell.DisplayValue ?? "";
                    entry.IsReadOnly = true;
                    entry.BackgroundColor = cell.HasError ? Colors.LightPink : Colors.White;
                }

                if (cell.HasError)
                {
                    ToolTipProperties.SetText(entry, cell.DisplayValue);
                }
                else
                {
                    ToolTipProperties.SetText(entry, null);
                }
            }
        }

        // Event handlers

        private async void OnModePickerChanged(object? sender, EventArgs e)
        {
            try
            {
                bool showExpression = ModePicker.SelectedIndex == 0;
                if (!showExpression)
                {
                    model.RecalculateAll();
                }
                RenderCells();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Не вдалося змінити режим: " + ex.Message, "OK");
            }
        }

        private async void OnEntryFocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            try
            {
                if (ModePicker.SelectedIndex != 0)
                {
                    ModePicker.SelectedIndex = 0;
                    MainThread.BeginInvokeOnMainThread(() => entry.Focus());
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Помилка фокусування: " + ex.Message, "OK");
            }
        }

        private async void OnCellEditCompleted(object? sender, EventArgs e)
        {
            if (sender is Entry entry && entry.BindingContext is string addr)
            {
                try
                {
                    if (model.Cells[addr].Expression != (entry.Text ?? ""))
                    {
                        model.Cells[addr].Expression = entry.Text ?? "";
                        model.RecalculateAll();
                    }
                    ModePicker.SelectedIndex = 1;
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Помилка обчислення", ex.Message, "OK");
                }
            }
        }

        private async void OnResizeClicked(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(RowsEntry.Text, out int newRows) || newRows <= 0 || newRows > 500)
                {
                    await DisplayAlert("Помилка", "Кількість рядків має бути додатнім числом (напр., 1-500).", "OK");
                    return;
                }
                if (!int.TryParse(ColsEntry.Text, out int newCols) || newCols <= 0 || newCols > 100)
                {
                    await DisplayAlert("Помилка", "Кількість стовпців має бути додатнім числом (напр., 1-100).", "OK");
                    return;
                }

                var oldModel = this.model;
                this.rows = newRows;
                this.cols = newCols;

                this.model = new SpreadsheetModel(newRows, newCols);
                this.model.CopyDataFrom(oldModel);

                BuildGrid();
                model.RecalculateAll();
                RenderCells();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Критична помилка", "Не вдалося змінити розмір: " + ex.Message, "OK");
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var path = Path.Combine(desktopPath, "spreadsheet_save.json");
                await SerializerHelper.SaveAsync(model, path);
                await DisplayAlert("Збережено", $"Файл збережено: {path}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Не вдалося зберегти файл: " + ex.Message, "OK");
            }
        }

        private async void OnLoadClicked(object sender, EventArgs e)
        {
            var options = new PickOptions
            {
                PickerTitle = "Оберіть файл",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                })
            };

            var result = await FilePicker.Default.PickAsync(options);
            
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var path = Path.Combine(desktopPath, "spreadsheet_save.json");
                var loaded = await SerializerHelper.LoadAsync<SpreadsheetModel>(path);
                if (loaded != null)
                {
                    this.model = loaded;
                    this.rows = loaded.Rows;
                    this.cols = loaded.Cols;
                    RowsEntry.Text = this.rows.ToString();
                    ColsEntry.Text = this.cols.ToString();

                    BuildGrid();
                    model.RecalculateAll();
                    RenderCells();

                    await DisplayAlert("Завантажено", $"Завантажено з {path}", "OK");
                }
                else
                {
                    await DisplayAlert("Помилка", $"Не знайдено файл {path}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Не вдалося завантажити файл: " + ex.Message, "OK");
            }
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            var text = "Довідка:\n\n" +
                "- Вводьте вирази в клітинки, напр.: 1+2, A1+3, inc(A2)\n" +
                "- Посилання: A1, B2, ...\n" +
                "- Функції: inc(x), dec(x)\n" +
                "- Операції: + - * / mod div\n" +
                "- Зміна виразу автоматично переобчислює залежні клітинки.\n" +
                "- Режим ВИРАЗ показує формули, ЗНАЧЕННЯ — обчислені значення.\n" +
                "- Використовуйте поля 'Рядки'/'Стовпці' та 'Змінити розмір' для редагування таблиці.\n" +
                "- Сувати таблицю можна за допомогою стрілочок на клавіатурі :)))";
            await DisplayAlert("Допомога", text, "OK");
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Про програму",
                "Лаб. робота №1\n\n" +
                "Виконав: Бондар Микола-Олег\n" +
                "Група: К-24\n" +
                "Варіант: 2 (Операції: 1, 2, 3, 5)",
                "OK");
        }

        private async void ExitButtonClicked(object sender, EventArgs e)
        {
            bool answear = await DisplayAlert("Підтвердження", "Ви дійсно хочете вийти?", "Так", "Ні");
            if (answear)
            {
                System.Environment.Exit(0);
            }
        }
    }
}