using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Um vao (porta/janela) da parede junto com as medidas que a deteccao automatica
    /// encontrou, guardadas para o botao "Restaurar automatico" e para saber se o vao
    /// foi ajustado. As medidas atuais ficam no proprio OpeningInfo (Vao).
    /// </summary>
    public sealed class VaoParaAjuste
    {
        public OpeningInfo Vao { get; }
        public string Titulo { get; }
        public bool EhJanela { get; }

        public double LarguraAutoInterna { get; }
        public double SoleiraAutoInterna { get; }
        public double AlturaAutoInterna { get; }

        public VaoParaAjuste(OpeningInfo vao, string titulo)
        {
            Vao = vao;
            Titulo = titulo;
            EhJanela = vao.Categoria == "Janela";
            LarguraAutoInterna = vao.LarguraInterna;
            SoleiraAutoInterna = vao.AlturaSoleiraInterna;
            AlturaAutoInterna = vao.AlturaInterna;
        }

        public double TopoAutoInterno
        {
            get { return SoleiraAutoInterna + AlturaAutoInterna; }
        }

        /// <summary>True se alguma medida atual do vao difere da deteccao automatica.</summary>
        public bool EstaAjustado()
        {
            const double tolerancia = 1e-6;
            return Math.Abs(Vao.LarguraInterna - LarguraAutoInterna) > tolerancia
                || Math.Abs(Vao.AlturaSoleiraInterna - SoleiraAutoInterna) > tolerancia
                || Math.Abs(Vao.AlturaInterna - AlturaAutoInterna) > tolerancia;
        }
    }

    /// <summary>
    /// Janela separada para ajustar os vaos um por um: lista de vaos de um lado e, do outro,
    /// largura / altura da soleira (so janela) / altura do topo do vao selecionado, em cm.
    /// As alteracoes so sao gravadas nos OpeningInfo ao clicar em Aplicar.
    /// </summary>
    public partial class AjustarVaosWindow : Window
    {
        internal static readonly CultureInfo CulturaBr = new CultureInfo("pt-BR");

        /// <summary>Estado de edicao de um vao enquanto a janela esta aberta.</summary>
        private sealed class LinhaEdicao
        {
            public VaoParaAjuste Item;
            public ListBoxItem ItemLista;

            // Valores de referencia (exatos, unidades internas) e o texto correspondente.
            // Um campo cujo texto continua igual ao da referencia mantem o valor exato;
            // so o que o usuario digita de diferente e convertido.
            public double BaseLargura;
            public double BaseSoleira;
            public double BaseTopo;
            public string BaseLarguraTxt;
            public string BaseSoleiraTxt;
            public string BaseTopoTxt;

            // Texto atual dos campos deste vao.
            public string LarguraTxt;
            public string SoleiraTxt;
            public string TopoTxt;
        }

        private readonly List<LinhaEdicao> _linhas = new List<LinhaEdicao>();
        private readonly double _alturaParedeCm;
        private LinhaEdicao _atual;
        private bool _carregandoCampos;

        /// <param name="vaos">Vaos da parede (editados no lugar se o usuario clicar em Aplicar).</param>
        /// <param name="alturaParedeCm">Altura da parede em cm (0 se desconhecida) - limite do topo do vao.</param>
        public AjustarVaosWindow(IList<VaoParaAjuste> vaos, double alturaParedeCm)
        {
            InitializeComponent();
            _alturaParedeCm = alturaParedeCm;

            foreach (VaoParaAjuste item in vaos)
            {
                var linha = new LinhaEdicao { Item = item };
                DefinirBase(
                    linha,
                    item.Vao.LarguraInterna,
                    item.Vao.AlturaSoleiraInterna,
                    item.Vao.AlturaSoleiraInterna + item.Vao.AlturaInterna);

                linha.ItemLista = new ListBoxItem { Tag = linha };
                _linhas.Add(linha);
                ListaVaos.Items.Add(linha.ItemLista);
                AtualizarRotulo(linha);
            }

            if (_linhas.Count > 0)
            {
                ListaVaos.SelectedIndex = 0;
            }
        }

        // ------------------------------------------------------------------
        // Utilitarios (tambem usados pela janela de opcoes)
        // ------------------------------------------------------------------

        internal static string FormatarCm(double cm)
        {
            return Math.Round(cm, 1).ToString("F1", CulturaBr);
        }

        /// <summary>Aceita virgula ou ponto como separador decimal.</summary>
        internal static bool TentarLerCm(string texto, out double valor)
        {
            string normalizado = (texto ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        // ------------------------------------------------------------------

        private static void DefinirBase(LinhaEdicao linha, double larguraInt, double soleiraInt, double topoInt)
        {
            linha.BaseLargura = larguraInt;
            linha.BaseSoleira = soleiraInt;
            linha.BaseTopo = topoInt;

            linha.BaseLarguraTxt = FormatarCm(RevitVersionAdapter.InternoParaCentimetros(larguraInt));
            linha.BaseSoleiraTxt = FormatarCm(RevitVersionAdapter.InternoParaCentimetros(soleiraInt));
            linha.BaseTopoTxt = FormatarCm(RevitVersionAdapter.InternoParaCentimetros(topoInt));

            linha.LarguraTxt = linha.BaseLarguraTxt;
            linha.SoleiraTxt = linha.BaseSoleiraTxt;
            linha.TopoTxt = linha.BaseTopoTxt;
        }

        /// <summary>
        /// Converte o texto atual dos campos de um vao em medidas internas. Devolve a mensagem
        /// de erro, ou null se estiver tudo certo. Campos nao mexidos mantem o valor exato da
        /// referencia; a coerencia (topo acima da soleira, topo abaixo da parede) so e
        /// cobrada do que o usuario de fato alterou.
        /// </summary>
        private string ResolverLinha(LinhaEdicao linha, out double larguraInt, out double soleiraInt, out double topoInt)
        {
            larguraInt = linha.BaseLargura;
            soleiraInt = linha.BaseSoleira;
            topoInt = linha.BaseTopo;

            bool larguraMexida = (linha.LarguraTxt ?? "").Trim() != linha.BaseLarguraTxt;
            bool soleiraMexida = linha.Item.EhJanela && (linha.SoleiraTxt ?? "").Trim() != linha.BaseSoleiraTxt;
            bool topoMexido = (linha.TopoTxt ?? "").Trim() != linha.BaseTopoTxt;

            double cm;

            if (larguraMexida)
            {
                if (!TentarLerCm(linha.LarguraTxt, out cm) || cm <= 0)
                    return "A largura precisa ser um numero maior que zero.";
                larguraInt = RevitVersionAdapter.CentimetrosParaInterno(cm);
            }

            if (soleiraMexida)
            {
                if (!TentarLerCm(linha.SoleiraTxt, out cm) || cm < 0)
                    return "A altura da soleira precisa ser um numero maior ou igual a zero.";
                soleiraInt = RevitVersionAdapter.CentimetrosParaInterno(cm);
            }

            if (topoMexido)
            {
                if (!TentarLerCm(linha.TopoTxt, out cm))
                    return "A altura do topo precisa ser um numero.";
                if (_alturaParedeCm > 0 && cm >= _alturaParedeCm)
                    return "O topo precisa ficar abaixo da altura da parede (" + FormatarCm(_alturaParedeCm) + " cm).";
                topoInt = RevitVersionAdapter.CentimetrosParaInterno(cm);
            }

            if ((soleiraMexida || topoMexido) && topoInt <= soleiraInt)
                return "O topo precisa ser maior que a altura da soleira.";

            return null;
        }

        private static bool Diferente(double a, double b)
        {
            return Math.Abs(a - b) > 1e-6;
        }

        /// <summary>Atualiza o texto do item na lista, marcando se o vao foi ajustado ou esta invalido.</summary>
        private void AtualizarRotulo(LinhaEdicao linha)
        {
            string sufixo = "";

            double largura, soleira, topo;
            if (ResolverLinha(linha, out largura, out soleira, out topo) != null)
            {
                sufixo = "   [valor invalido]";
            }
            else if (Diferente(largura, linha.Item.LarguraAutoInterna)
                || Diferente(soleira, linha.Item.SoleiraAutoInterna)
                || Diferente(topo, linha.Item.TopoAutoInterno))
            {
                sufixo = "   [ajustado]";
            }

            linha.ItemLista.Content = new TextBlock
            {
                Text = linha.Item.Titulo + sufixo,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void CarregarCampos(LinhaEdicao linha)
        {
            _carregandoCampos = true;
            try
            {
                if (linha == null)
                {
                    TextTituloVao.Text = "";
                    TextAuto.Text = "";
                    TxtLargura.Text = "";
                    TxtSoleira.Text = "";
                    TxtTopo.Text = "";
                    TxtLargura.IsEnabled = false;
                    TxtSoleira.IsEnabled = false;
                    TxtTopo.IsEnabled = false;
                    BtnRestaurar.IsEnabled = false;
                    return;
                }

                VaoParaAjuste item = linha.Item;

                TextTituloVao.Text = item.Titulo;
                TxtLargura.Text = linha.LarguraTxt;
                TxtSoleira.Text = linha.SoleiraTxt;
                TxtTopo.Text = linha.TopoTxt;

                TxtLargura.IsEnabled = true;
                TxtTopo.IsEnabled = true;
                BtnRestaurar.IsEnabled = true;

                TxtSoleira.IsEnabled = item.EhJanela;
                LabelSoleira.Text = item.EhJanela
                    ? "Altura da soleira (cm):"
                    : "Altura da soleira (so janela):";

                TextAuto.Text = "Automatico: largura "
                    + FormatarCm(RevitVersionAdapter.InternoParaCentimetros(item.LarguraAutoInterna))
                    + " | soleira "
                    + FormatarCm(RevitVersionAdapter.InternoParaCentimetros(item.SoleiraAutoInterna))
                    + " | topo "
                    + FormatarCm(RevitVersionAdapter.InternoParaCentimetros(item.TopoAutoInterno));
            }
            finally
            {
                _carregandoCampos = false;
            }
        }

        private void ListaVaos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem selecionado = ListaVaos.SelectedItem as ListBoxItem;
            _atual = selecionado != null ? selecionado.Tag as LinhaEdicao : null;
            CarregarCampos(_atual);
        }

        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoCampos || _atual == null)
                return;

            _atual.LarguraTxt = TxtLargura.Text;
            _atual.SoleiraTxt = TxtSoleira.Text;
            _atual.TopoTxt = TxtTopo.Text;

            AtualizarRotulo(_atual);
        }

        private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
        {
            if (_atual == null)
                return;

            VaoParaAjuste item = _atual.Item;
            DefinirBase(_atual, item.LarguraAutoInterna, item.SoleiraAutoInterna, item.TopoAutoInterno);
            CarregarCampos(_atual);
            AtualizarRotulo(_atual);
        }

        private void BtnAplicar_Click(object sender, RoutedEventArgs e)
        {
            // Primeiro valida todos os vaos; so grava se estiver tudo certo.
            var resolvidos = new List<double[]>();

            foreach (LinhaEdicao linha in _linhas)
            {
                double largura, soleira, topo;
                string erro = ResolverLinha(linha, out largura, out soleira, out topo);

                if (erro != null)
                {
                    ListaVaos.SelectedItem = linha.ItemLista;
                    MessageBox.Show(
                        linha.Item.Titulo + "\n\n" + erro,
                        "LSF Modeler - Vaos",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                resolvidos.Add(new[] { largura, soleira, topo });
            }

            for (int i = 0; i < _linhas.Count; i++)
            {
                OpeningInfo vao = _linhas[i].Item.Vao;
                double[] valores = resolvidos[i];

                vao.LarguraInterna = valores[0];
                vao.AlturaSoleiraInterna = valores[1];
                vao.AlturaInterna = valores[2] - valores[1];
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}