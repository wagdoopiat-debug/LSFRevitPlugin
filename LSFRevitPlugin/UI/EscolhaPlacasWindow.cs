using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Janela de escolha das placas: uma linha por painel, com a caixa "Refazer" e o tipo, a
    /// medida e a orientacao de cada face. Feita em codigo (sem XAML).
    /// </summary>
    public sealed class EscolhaPlacasWindow : Window
    {
        private sealed class ItemTipo
        {
            public TipoPlaca Tipo;
            public override string ToString() { return PlacaCatalogo.NomeDoTipo(Tipo); }
        }

        private sealed class ItemOrientacao
        {
            public OrientacaoPlaca Orientacao;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        /// <summary>Os tres seletores (tipo, medida, orientacao) de uma face.</summary>
        private sealed class ControlesFace
        {
            public readonly ComboBox Tipo = new ComboBox { Width = 130, Margin = new Thickness(2) };
            public readonly ComboBox Medida = new ComboBox { Width = 95, Margin = new Thickness(2), DisplayMemberPath = "Descricao" };
            public readonly ComboBox Orientacao = new ComboBox { Width = 80, Margin = new Thickness(2) };

            public ControlesFace(TipoPlaca tipoInicial)
            {
                foreach (TipoPlaca t in PlacaCatalogo.TodosOsTipos())
                {
                    var item = new ItemTipo { Tipo = t };
                    Tipo.Items.Add(item);
                    if (t == tipoInicial) Tipo.SelectedItem = item;
                }

                Orientacao.Items.Add(new ItemOrientacao { Orientacao = OrientacaoPlaca.EmPe, Nome = "Em pe" });
                Orientacao.Items.Add(new ItemOrientacao { Orientacao = OrientacaoPlaca.Deitada, Nome = "Deitada" });
                Orientacao.SelectedIndex = 0;

                PreencherMedidas();
                Tipo.SelectionChanged += (s, e) => PreencherMedidas();
            }

            public void PreencherMedidas()
            {
                ItemTipo item = Tipo.SelectedItem as ItemTipo;
                if (item == null) return;

                MedidaPlaca anterior = Medida.SelectedItem as MedidaPlaca;

                Medida.Items.Clear();
                foreach (MedidaPlaca m in PlacaCatalogo.Padrao.MedidasDisponiveis(item.Tipo))
                {
                    Medida.Items.Add(m);
                }

                MedidaPlaca igual = anterior == null
                    ? null
                    : Medida.Items.Cast<MedidaPlaca>().FirstOrDefault(m => m.Equals(anterior));
                object escolhido = igual != null ? (object)igual : Medida.Items[0];
                Medida.SelectedItem = escolhido;
            }

            public void Copiar(ControlesFace de)
            {
                Tipo.SelectedIndex = de.Tipo.SelectedIndex;
                Medida.SelectedIndex = de.Medida.SelectedIndex;
                Orientacao.SelectedIndex = de.Orientacao.SelectedIndex;
            }

            /// <summary>Seleciona nos tres campos o que uma escolha ja existente usa.</summary>
            public void Definir(EscolhaPlaca escolha)
            {
                foreach (object item in Tipo.Items)
                {
                    if (((ItemTipo)item).Tipo == escolha.Tipo)
                    {
                        Tipo.SelectedItem = item;
                        break;
                    }
                }

                PreencherMedidas();

                foreach (object medida in Medida.Items)
                {
                    if (((MedidaPlaca)medida).Equals(escolha.Medida))
                    {
                        Medida.SelectedItem = medida;
                        break;
                    }
                }

                foreach (object orientacao in Orientacao.Items)
                {
                    if (((ItemOrientacao)orientacao).Orientacao == escolha.Orientacao)
                    {
                        Orientacao.SelectedItem = orientacao;
                        break;
                    }
                }
            }

            public EscolhaPlaca Obter()
            {
                return new EscolhaPlaca
                {
                    Tipo = ((ItemTipo)Tipo.SelectedItem).Tipo,
                    Medida = (MedidaPlaca)Medida.SelectedItem,
                    Orientacao = ((ItemOrientacao)Orientacao.SelectedItem).Orientacao
                };
            }
        }

        private sealed class Linha
        {
            public LeituraParede Leitura;
            public PainelInfo Painel;
            public CheckBox Refazer;
            public ControlesFace Face1;
            public ControlesFace Face2;
        }

        private readonly List<Linha> _linhas = new List<Linha>();
        private readonly ControlesFace _todosFace1 = new ControlesFace(TipoPlaca.Cimenticia);
        private readonly ControlesFace _todosFace2 = new ControlesFace(TipoPlaca.GessoSt);

        /// <summary>true se o usuario clicou em "Gerar placas".</summary>
        public bool Confirmado { get; private set; }

        /// <summary>Devolve a escolha de uma face de um painel. So vale depois de Confirmado.</summary>
        public Func<LeituraParede, PainelInfo, int, EscolhaPlaca> Seletor { get; private set; }

        /// <summary>Diz se o painel foi marcado para refazer. So vale depois de Confirmado.</summary>
        public Func<LeituraParede, PainelInfo, bool> Refazer { get; private set; }

        public EscolhaPlacasWindow(IList<LeituraParede> leituras, PlacasExistentes existentes)
        {
            Title = "LSF Modeler - Placas";
            Width = 1150;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var raiz = new DockPanel { Margin = new Thickness(10), LastChildFill = true };

            // ---- Topo: aplicar aos paineis marcados ----
            var topo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            topo.Children.Add(Texto("Face 1:"));
            topo.Children.Add(_todosFace1.Tipo);
            topo.Children.Add(_todosFace1.Medida);
            topo.Children.Add(_todosFace1.Orientacao);
            topo.Children.Add(Texto("   Face 2:"));
            topo.Children.Add(_todosFace2.Tipo);
            topo.Children.Add(_todosFace2.Medida);
            topo.Children.Add(_todosFace2.Orientacao);

            var botaoAplicar = new Button { Content = "Aplicar aos marcados", Margin = new Thickness(8, 2, 2, 2), Padding = new Thickness(10, 2, 10, 2) };
            botaoAplicar.Click += (s, e) =>
            {
                foreach (Linha linha in _linhas.Where(l => l.Refazer.IsChecked == true))
                {
                    linha.Face1.Copiar(_todosFace1);
                    linha.Face2.Copiar(_todosFace2);
                }
            };
            topo.Children.Add(botaoAplicar);

            DockPanel.SetDock(topo, Dock.Top);
            raiz.Children.Add(topo);

            var marcacao = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var botaoMarcar = new Button { Content = "Marcar todos", Margin = new Thickness(2), Padding = new Thickness(10, 2, 10, 2) };
            var botaoDesmarcar = new Button { Content = "Desmarcar todos", Margin = new Thickness(2), Padding = new Thickness(10, 2, 10, 2) };
            botaoMarcar.Click += (s, e) => { foreach (Linha linha in _linhas) linha.Refazer.IsChecked = true; };
            botaoDesmarcar.Click += (s, e) => { foreach (Linha linha in _linhas) linha.Refazer.IsChecked = false; };
            marcacao.Children.Add(botaoMarcar);
            marcacao.Children.Add(botaoDesmarcar);
            marcacao.Children.Add(Texto("Painéis desmarcados mantêm as placas que já têm."));
            DockPanel.SetDock(marcacao, Dock.Top);
            raiz.Children.Add(marcacao);

            // ---- Rodape: botoes ----
            var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var botaoGerar = new Button { Content = "Gerar placas", IsDefault = true, Margin = new Thickness(4), Padding = new Thickness(16, 4, 16, 4) };
            var botaoCancelar = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(4), Padding = new Thickness(16, 4, 16, 4) };
            botaoGerar.Click += (s, e) => Confirmar();
            botoes.Children.Add(botaoGerar);
            botoes.Children.Add(botaoCancelar);
            DockPanel.SetDock(botoes, Dock.Bottom);
            raiz.Children.Add(botoes);

            // ---- Cadastro de nova medida ----
            var cadastro = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var tipoCadastro = new ComboBox { Width = 130, Margin = new Thickness(2) };
            foreach (TipoPlaca t in PlacaCatalogo.TodosOsTipos())
            {
                tipoCadastro.Items.Add(new ItemTipo { Tipo = t });
            }
            tipoCadastro.SelectedIndex = 0;
            var lado1 = new TextBox { Width = 60, Margin = new Thickness(2), Text = "1200" };
            var lado2 = new TextBox { Width = 60, Margin = new Thickness(2), Text = "2400" };
            var botaoCadastrar = new Button { Content = "Cadastrar medida", Margin = new Thickness(6, 2, 2, 2), Padding = new Thickness(10, 2, 10, 2) };
            botaoCadastrar.Click += (s, e) =>
            {
                double v1;
                double v2;
                if (!double.TryParse(lado1.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out v1)
                    || !double.TryParse(lado2.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out v2))
                {
                    MessageBox.Show(this, "Digite os dois lados da placa em mm, so numeros (por exemplo 1200 e 2400).", "Placas");
                    return;
                }

                string erro;
                TipoPlaca tipo = ((ItemTipo)tipoCadastro.SelectedItem).Tipo;
                if (!PlacaCatalogo.Padrao.Cadastrar(tipo, v1, v2, out erro))
                {
                    MessageBox.Show(this, erro, "Placas");
                    return;
                }

                _todosFace1.PreencherMedidas();
                _todosFace2.PreencherMedidas();
                foreach (Linha linha in _linhas)
                {
                    linha.Face1.PreencherMedidas();
                    linha.Face2.PreencherMedidas();
                }

                MessageBox.Show(this, "Medida cadastrada. Ela ja aparece nas listas de medida.", "Placas");
            };

            cadastro.Children.Add(Texto("Cadastrar nova medida (mm) - tipo:"));
            cadastro.Children.Add(tipoCadastro);
            cadastro.Children.Add(lado1);
            cadastro.Children.Add(Texto("x"));
            cadastro.Children.Add(lado2);
            cadastro.Children.Add(botaoCadastrar);
            DockPanel.SetDock(cadastro, Dock.Bottom);
            raiz.Children.Add(cadastro);

            // ---- Centro: uma linha por painel ----
            var grade = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            for (int c = 0; c < 8; c++)
            {
                grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            string[] cabecalhos =
            {
                "Refazer", "Painel", "Face 1 - tipo", "medida", "orientacao", "Face 2 - tipo", "medida", "orientacao"
            };

            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < cabecalhos.Length; c++)
            {
                TextBlock cab = Texto(cabecalhos[c]);
                cab.FontWeight = FontWeights.Bold;
                Colocar(grade, cab, 0, c);
            }

            int linhaAtual = 1;
            foreach (LeituraParede leitura in leituras.Where(l => l.TemEstrutura))
            {
                int paredeId = leitura.Parede.Id.IntegerValue;

                foreach (PainelInfo painel in leitura.Paineis)
                {
                    double larguraMm = RevitVersionAdapter.InternoParaCentimetros(
                        painel.FimInterno - painel.InicioInterno) * 10.0;

                    var linha = new Linha
                    {
                        Leitura = leitura,
                        Painel = painel,
                        Refazer = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
                        Face1 = new ControlesFace(TipoPlaca.Cimenticia),
                        Face2 = new ControlesFace(TipoPlaca.GessoSt)
                    };

                    // O que o painel usa hoje. Se faltar dado de alguma face (placas de uma versao
                    // antiga) ou nao ha placas, o painel ja vem marcado para refazer.
                    EscolhaPlaca atual1 = existentes.Escolha(paredeId, painel.Numero, 1);
                    EscolhaPlaca atual2 = existentes.Escolha(paredeId, painel.Numero, 2);
                    if (atual1 != null) linha.Face1.Definir(atual1);
                    if (atual2 != null) linha.Face2.Definir(atual2);
                    linha.Refazer.IsChecked = atual1 == null || atual2 == null;

                    _linhas.Add(linha);

                    string situacao = existentes.TemPlacas(paredeId, painel.Numero) ? "com placas" : "sem placas";

                    grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Colocar(grade, linha.Refazer, linhaAtual, 0);
                    Colocar(
                        grade,
                        Texto("Parede " + paredeId + " - painel " + painel.Numero
                            + " (" + larguraMm.ToString("0") + " mm, " + situacao + ")"),
                        linhaAtual, 1);
                    Colocar(grade, linha.Face1.Tipo, linhaAtual, 2);
                    Colocar(grade, linha.Face1.Medida, linhaAtual, 3);
                    Colocar(grade, linha.Face1.Orientacao, linhaAtual, 4);
                    Colocar(grade, linha.Face2.Tipo, linhaAtual, 5);
                    Colocar(grade, linha.Face2.Medida, linhaAtual, 6);
                    Colocar(grade, linha.Face2.Orientacao, linhaAtual, 7);
                    linhaAtual++;
                }
            }

            var rolagem = new ScrollViewer
            {
                Content = grade,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            raiz.Children.Add(rolagem);

            Content = raiz;
        }

        private void Confirmar()
        {
            var escolhas = new Dictionary<string, EscolhaPlaca>();
            var refazer = new Dictionary<string, bool>();

            foreach (Linha linha in _linhas)
            {
                escolhas[Chave(linha.Leitura, linha.Painel.Numero, 1)] = linha.Face1.Obter();
                escolhas[Chave(linha.Leitura, linha.Painel.Numero, 2)] = linha.Face2.Obter();
                refazer[Chave(linha.Leitura, linha.Painel.Numero, 0)] = linha.Refazer.IsChecked == true;
            }

            Seletor = (leitura, painel, face) => escolhas[Chave(leitura, painel.Numero, face)];
            Refazer = (leitura, painel) =>
            {
                bool marcado;
                return refazer.TryGetValue(Chave(leitura, painel.Numero, 0), out marcado) && marcado;
            };

            Confirmado = true;
            Close();
        }

        private static string Chave(LeituraParede leitura, int numeroPainel, int face)
        {
            return leitura.Parede.Id.IntegerValue + "|" + numeroPainel + "|" + face;
        }

        private static TextBlock Texto(string texto)
        {
            return new TextBlock
            {
                Text = texto,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 4, 2)
            };
        }

        private static void Colocar(Grid grade, UIElement elemento, int linha, int coluna)
        {
            Grid.SetRow(elemento, linha);
            Grid.SetColumn(elemento, coluna);
            grade.Children.Add(elemento);
        }
    }
}