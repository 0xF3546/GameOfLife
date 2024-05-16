using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameOfLife
{
    public partial class MainWindow : Window
    {
        class Board
        {
            public int height = 0;
            public int width = 0;
            public GOLCanvas[][] field;
            public string String = "";
            public bool isImportingExporting = false;

            public void Export(string filename)
            {
                isImportingExporting = true;
                string json = JsonConvert.SerializeObject(this);
                if (!File.Exists(filename))
                {
                    File.Create(filename);
                }
                File.WriteAllText(filename, json);
                isImportingExporting = false;
            }
            public bool Load(string filename)
            {
                isImportingExporting = true;
                Board board = this;
                string json = File.ReadAllText(filename);
                board = JsonConvert.DeserializeObject<Board>(json);
                isImportingExporting = false;
                return true;
            }
        }
        Board board;
        private readonly Canvas parentCanvas = new Canvas();
        private int lastInsertedId = 0;
        private List<GOLCanvas> canvases = new List<GOLCanvas>();
        private readonly Random random = new Random();

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private async Task Tick()
        {
            while (!board.isImportingExporting)
            {
                await AddCanvas(random.Next(0, board.height), random.Next(0, board.width));
                await LoopGamefield();

                await Task.Delay(15);
            }
        }

        private int x = 0;
        private int y = 0;
        public async Task CellTickAsync()
        {
            while (!cancellation.Token.IsCancellationRequested)
            {
                Console.WriteLine($"{x}/{board.height} {y}/{board.width}");
                await InitStartingCell(x, y);

                y++;
                if (y >= board.width)
                {
                    y = 0;
                    x++;
                }

                if (x >= board.height)
                {
                    cancellation.Cancel();
                    _ = Tick();
                    return;
                }
                await Task.Delay(0, cancellation.Token);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Name = "overAll";
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HeightTextBox.Text, out int height) && int.TryParse(WidthTextBox.Text, out int width))
            {
                _ = SetupAsync(height, width);
            }
            else
            {
                MessageBox.Show("Please enter valid numeric values for height and width.");
            }
        }


        public Task SetupAsync(int width, int height)
        {
            board = new Board
            {
                width = width,
                height = height
            };
            Height = height * 15;
            Width = width * 15;
            board.field = new GOLCanvas[width][];

            for (int i = 0; i < width; i++)
            {
                board.field[i] = new GOLCanvas[height];
            }

            _ = CellTickAsync();

            Title = "Game of Life";
            parentCanvas.Width = width * 5;
            parentCanvas.Height = height * 5;

            Canvas overallCanvas = new Canvas();

            Button exportButton = new Button();
            exportButton.Width = 65;
            exportButton.Height = 30;
            exportButton.Content = "Export";
            exportButton.Click += ExportButton_Click;
            Canvas.SetLeft(exportButton, 10);
            Canvas.SetTop(exportButton, 10);

            Button importButton = new Button();
            importButton.Width = 65;
            importButton.Height = 30;
            importButton.Content = "Import";
            importButton.Click += ImportButton_Click;
            Canvas.SetLeft(importButton, 85);
            Canvas.SetTop(importButton, 10);

            Canvas.SetLeft(parentCanvas, 0);
            Canvas.SetTop(parentCanvas, 50);

            overallCanvas.Children.Add(exportButton);
            overallCanvas.Children.Add(importButton);
            overallCanvas.Children.Add(parentCanvas);

            Content = overallCanvas;
            return Task.CompletedTask;

        }

        private void Stamp(Board board, string stamp, int x, int y)
        {

        }

        private async Task InitStartingCell(int x, int y)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                GOLCanvas canvas = new GOLCanvas
                {
                    X = x,
                    Y = y
                };

                Canvas.SetLeft(canvas, x * 5);
                Canvas.SetTop(canvas, y * 5);
                canvas.ClearCell();

                parentCanvas.Children.Add(canvas);

                board.field[x][y] = canvas;
                if (random.Next(6) == 0)
                {
                    canvas.FillCell();
                }
            });
        }

        public Task AddCanvas(int x, int y)
        {
            if (x < 0 || x >= board.width || y < 0 || y >= board.height || board.field[x][y] == null)
            {
                return Task.CompletedTask;
            }

            Console.WriteLine($"Adding Canvas x/{x} y/{y}");
            GOLCanvas canvas = board.field[x][y];
            canvas.Id = lastInsertedId;
            Canvas.SetLeft(canvas, x * 5);
            Canvas.SetTop(canvas, y * 5);
            canvas.X = x;
            canvas.Y = y;
            canvas.FillCell();
            canvases.Add(canvas);
            board.field[x][y] = canvas;
            lastInsertedId++;
            return Task.CompletedTask;
        }

        public Task LoopGamefield()
        {
            var updatedCanvases = new List<GOLCanvas>(canvases);

            foreach (var canvas in canvases)
            {
                var neighbours = GetNeighbours(canvas.X, canvas.Y);
                int filledNeighbours = neighbours.Count(n => n.IsFilled());

                if (canvas.IsFilled() && (filledNeighbours < 2 || filledNeighbours > 3))
                {
                    var cellToClear = updatedCanvases.FirstOrDefault(c => c.Id == canvas.Id);
                    if (cellToClear != null)
                    {
                        cellToClear.SetVariable("shouldBeCleared", true);
                    }
                }
                else if (!canvas.IsFilled() && filledNeighbours == 3)
                {
                    var cellToFill = updatedCanvases.FirstOrDefault(c => c.Id == canvas.Id);
                    if (cellToFill != null)
                    {
                        cellToFill.SetVariable("shouldBeFilled", true);
                    }
                }
            }

            foreach (var canvas in updatedCanvases)
            {
                if (canvas.GetVariable<bool>("shouldBeCleared"))
                {
                    canvas.ClearCell();
                }
                else if (canvas.GetVariable<bool>("shouldBeFilled"))
                {
                    canvas.FillCell();
                }

                canvas.SetVariable("shouldBeCleared", false);
                canvas.SetVariable("shouldBeFilled", false);
            }

            canvases = updatedCanvases;
            return Task.CompletedTask;
        }


        private IList<GOLCanvas> GetNeighbours(int x, int y)
        {
            var neighbours = new List<GOLCanvas>();

            for (var i = -1; i <= 1; i++)
            {
                for (var j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0
                        || x + i < 0 || x + i >= board.width
                        || y + j < 0 || y + j >= board.height) continue;

                    var neighbour = board.field[x + i][y + j];
                    if (neighbour.IsFilled())
                    {
                        neighbours.Add(neighbour);
                    }
                }
            }
            return neighbours;
        }

        public List<GOLCanvas> GetCanvasInRange(int centerX, int centerY, int range)
        {
            List<GOLCanvas> canvasesInRange = new List<GOLCanvas>();

            for (int x = Math.Max(0, centerX - range); x < Math.Min(board.width, centerX + range); x++)
            {
                for (int y = Math.Max(0, centerY - range); y < Math.Min(board.height, centerY + range); y++)
                {
                    canvasesInRange.Add(board.field[x][y]);
                }
            }
            return canvasesInRange;
        }


        public Task RemoveCanvas(object canvas)
        {
            GOLCanvas canvasToRemove = null;

            if (canvas.GetType() == typeof(GOLCanvas))
            {
                canvasToRemove = (GOLCanvas)canvas;
            }

            else if (canvas is int canvasId)
            {
                canvasToRemove = canvases.FirstOrDefault(c => c.Id == canvasId);
            }

            if (canvasToRemove == null)
            {
                return Task.CompletedTask;
            }

            List<GOLCanvas> copy = new List<GOLCanvas>(canvases);
            copy.Remove(canvasToRemove);
            canvases = copy;

            canvasToRemove.ClearCell();
            return Task.CompletedTask;
        }

        public GOLCanvas GetCanvasById(int id)
        {
            foreach (GOLCanvas canvas in canvases)
            {
                if (canvas.Id == id) return canvas;
            }
            return canvases[id];
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string filename = "board.json"; 
            board.Export(filename);
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            string filename = "board.json";
            bool loaded = board.Load(filename);
            if (!loaded)
            {
            }
        }

    }

    public class GOLCanvas : Canvas
    {
        private readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        private bool isFilled = false;

        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        private Border border;

        public void SetVariable<T>(string key, T value)
        {
            Values[key] = value;
        }

        public T GetVariable<T>(string key)
        {
            if (Values.TryGetValue(key, out var value))
            {
                return (T)value;
            }
            return default;
        }

        public void FillCell()
        {
            isFilled = true;
            Background = Brushes.Black;
        }

        public void ClearCell()
        {
            isFilled = false;
            Background = Brushes.Transparent;
        }

        public bool IsFilled()
        {
            return isFilled;
        }

        public GOLCanvas()
        {
            Width = 5;
            Height = 5;
            Application.Current.Dispatcher.Invoke(() =>
            {
                border = new Border();
                Background = Brushes.Transparent;
                border.BorderBrush = Brushes.Black;
                border.BorderThickness = new Thickness(1);
                Children.Add(border);
            });
        }
    }

}
