using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Mostra o quantitativo do projeto (uma aba por tabela) antes de gerar o CSV. O botao
    /// "Gerar CSV" e o botao "Criar parafusos no 3D" fecham a janela e marcam a escolha; quem abriu
    /// a janela faz o trabalho.
    /// </summary>
    public sealed class QuantitativoWindow : Window
    {
        public bool GerarCsv { get; private set; }
        public bool CriarParafusos3D { get; private set; }

        public QuantitativoWindow(DadosQuantitativo dados, IntPtr donoHandle)
        {
            Title = "LSF Modeler - Quantitativo";
            Width = 1000;
            Height = 650;
            MinWidth = 700;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            if (donoHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(this).Owner = donoHandle;
            }

            var raiz = new Grid { Margin = new Thickness(10) };
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---- Cabecalho: resumo e avisos ----
            var cabecalho = new StackPanel();

            cabecalho.Children.Add(new TextBlock
            {
                Text = "Projeto: " + dados.Projeto + "   |   " + dados.DataHora,
                FontWeight = FontWeights.SemiBold
            });

            cabecalho.Children.Add(new TextBlock
            {
                Text = dados.ElementosEstrutura + " elementos de estrutura (" + dados.ComprimentoTotalM.ToString("0.0")
                    + " m de perfil)   |   " + dados.PecasDePlaca + " peças de placa, " + dados.PlacasUsadas
                    + " placas usadas, " + dados.Recortes + " recortes   |   parafusos: "
                    + dados.ParafusosEstrutura + " na estrutura, " + dados.ParafusosPlacas + " nas placas",
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            cabecalho.Children.Add(new TextBlock
            {
                Text = dados.Observacao + " Selecione linhas e use Ctrl+C para copiar para o Excel.",
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            if (dados.Avisos.Count > 0)
            {
                cabecalho.Children.Add(new TextBox
                {
                    Text = string.Join(Environment.NewLine, dados.Avisos),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 70,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }

            Grid.SetRow(cabecalho, 0);
            raiz.Children.Add(cabecalho);

            // ---- Abas ----
            var abas = new TabControl { Margin = new Thickness(0, 8, 0, 8) };

            foreach (SecaoQuantitativo secao in dados.Secoes)
            {
                if (secao.SoNoCsv || secao.Colunas == null) continue;

                abas.Items.Add(new TabItem
                {
                    Header = secao.Aba ?? secao.Titulo,
                    Content = CriarGrade(secao)
                });
            }

            Grid.SetRow(abas, 1);
            raiz.Children.Add(abas);

            // ---- Botoes ----
            var botoes = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var gerar = new Button
            {
                Content = "Gerar CSV",
                MinWidth = 100,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            gerar.Click += (remetente, args) =>
            {
                GerarCsv = true;
                Close();
            };

            var parafusos3D = new Button
            {
                Content = "Criar parafusos no 3D",
                MinWidth = 100,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 8, 0)
            };
            parafusos3D.Click += (remetente, args) =>
            {
                CriarParafusos3D = true;
                Close();
            };

            var fechar = new Button
            {
                Content = "Fechar",
                MinWidth = 100,
                Padding = new Thickness(12, 4, 12, 4),
                IsCancel = true
            };

            botoes.Children.Add(gerar);
            botoes.Children.Add(parafusos3D);
            botoes.Children.Add(fechar);

            Grid.SetRow(botoes, 2);
            raiz.Children.Add(botoes);

            Content = raiz;
        }

        private static DataGrid CriarGrade(SecaoQuantitativo secao)
        {
            var grade = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserSortColumns = false,
                CanUserReorderColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
                RowBackground = Brushes.White,
                AlternatingRowBackground = Brushes.WhiteSmoke,
                ItemsSource = secao.Linhas
            };

            var estiloTexto = new Style(typeof(TextBlock));
            estiloTexto.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            estiloTexto.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2)));

            for (int i = 0; i < secao.Colunas.Length; i++)
            {
                bool ultima = i == secao.Colunas.Length - 1;

                grade.Columns.Add(new DataGridTextColumn
                {
                    Header = secao.Colunas[i],
                    Binding = new Binding("[" + i + "]") { Mode = BindingMode.OneWay },
                    ElementStyle = estiloTexto,
                    Width = secao.UltimaColunaLarga && ultima
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : DataGridLength.Auto
                });
            }

            return grade;
        }
    }
}