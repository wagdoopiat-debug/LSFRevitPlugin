using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using XYZ = Autodesk.Revit.DB.XYZ;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Janela que pede sistema (Drywall/Steel Frame), largura, bitola e espacamento antes
    /// de gerar a estrutura. Abre pre-preenchida com a ultima escolha do usuario.
    /// Quando a parede tem portas/janelas, mostra o botao "Ajustar vaos..." que abre a janela
    /// separada (AjustarVaosWindow) para editar as medidas de cada vao. Os ajustes de vao
    /// valem so para a geracao em curso (nao sao salvos).
    /// </summary>
    public partial class OpcoesGeracaoWindow : Window
    {
        private readonly List<VaoParaAjuste> _vaosParaAjuste = new List<VaoParaAjuste>();
        private readonly double _alturaParedeCm;

        /// <summary>Perfil escolhido. So valido se DialogResult for true.</summary>
        public PerfilSpec PerfilEscolhido { get; private set; }

        /// <summary>Espacamento escolhido, em cm. So valido se DialogResult for true.</summary>
        public double EspacamentoEscolhidoCm { get; private set; }

        /// <summary>
        /// Quantos vaos tem medidas diferentes da deteccao automatica. Os ajustes sao gravados
        /// direto nos OpeningInfo recebidos no construtor. So valido se DialogResult for true.
        /// </summary>
        public int QuantidadeVaosAjustados { get; private set; }

        public OpcoesGeracaoWindow() : this(null, null, null, 0)
        {
        }

        /// <param name="vaos">Vaos detectados na parede (editados no lugar se o usuario ajustar).</param>
        /// <param name="inicioParede">Ponto inicial do eixo da parede (para mostrar a posicao de cada vao).</param>
        /// <param name="direcaoParede">Direcao unitaria do eixo da parede.</param>
        /// <param name="alturaParedeCm">Altura da parede em cm (0 se desconhecida) - limite do topo do vao.</param>
        public OpcoesGeracaoWindow(
            IList<OpeningInfo> vaos, XYZ inicioParede, XYZ direcaoParede, double alturaParedeCm)
        {
            InitializeComponent();
            _alturaParedeCm = alturaParedeCm;
            CarregarUltimaSelecao();
            PrepararVaos(vaos, inicioParede, direcaoParede);
        }

        private void CarregarUltimaSelecao()
        {
            var (perfil, espacamentoCm) = PreferenciasGeracao.CarregarUltimaSelecaoOuPadrao();

            if (perfil.Sistema == SistemaPerfil.Drywall)
            {
                RadioSistemaDrywall.IsChecked = true;
                SelecionarRadioLargura(PanelLarguraDrywall, perfil.LarguraAlmaMm);
            }
            else
            {
                RadioSistemaSteelFrame.IsChecked = true;
                SelecionarRadioLargura(PanelLarguraSteelFrame, perfil.LarguraAlmaMm);

                if (perfil.EspessuraChapaMm == 0.80) RadioBitola080.IsChecked = true;
                else if (perfil.EspessuraChapaMm == 1.25) RadioBitola125.IsChecked = true;
                else RadioBitola095.IsChecked = true;
            }

            AtualizarPaineisVisiveis();

            RadioBtn60.IsChecked = espacamentoCm == 60.0;
            RadioBtn40.IsChecked = espacamentoCm != 60.0;
        }

        private void SelecionarRadioLargura(StackPanel painel, double larguraMm)
        {
            foreach (object filho in painel.Children)
            {
                if (filho is RadioButton radio)
                {
                    string textoEsperado = larguraMm.ToString("F0") + " mm";
                    radio.IsChecked = radio.Content?.ToString() == textoEsperado;
                }
            }
        }

        private void RadioSistema_Checked(object sender, RoutedEventArgs e)
        {
            AtualizarPaineisVisiveis();
        }

        private void AtualizarPaineisVisiveis()
        {
            bool ehDrywall = RadioSistemaDrywall.IsChecked == true;

            PanelLarguraDrywall.Visibility = ehDrywall ? Visibility.Visible : Visibility.Collapsed;
            PanelLarguraSteelFrame.Visibility = ehDrywall ? Visibility.Collapsed : Visibility.Visible;

            PanelBitolaSteelFrame.Visibility = ehDrywall ? Visibility.Collapsed : Visibility.Visible;
            TextBitolaFixaDrywall.Visibility = ehDrywall ? Visibility.Visible : Visibility.Collapsed;

            if (ehDrywall && PanelLarguraDrywall.Children.Count > 0
                && !TemAlgumSelecionado(PanelLarguraDrywall))
            {
                RadioLargura48DW.IsChecked = true;
            }
            if (!ehDrywall && !TemAlgumSelecionado(PanelLarguraSteelFrame))
            {
                RadioLargura90SF.IsChecked = true;
            }
            if (!ehDrywall && !TemAlgumSelecionado(PanelBitolaSteelFrame))
            {
                RadioBitola095.IsChecked = true;
            }
        }

        private bool TemAlgumSelecionado(StackPanel painel)
        {
            foreach (object filho in painel.Children)
            {
                if (filho is RadioButton radio && radio.IsChecked == true)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Vaos (portas/janelas)
        // ------------------------------------------------------------------

        private void PrepararVaos(IList<OpeningInfo> vaos, XYZ inicioParede, XYZ direcaoParede)
        {
            if (vaos == null || vaos.Count == 0 || inicioParede == null || direcaoParede == null)
                return;

            int numero = 1;
            foreach (OpeningInfo vao in vaos)
            {
                double distanciaCm = RevitVersionAdapter.InternoParaCentimetros(
                    (vao.PontoCentral - inicioParede).DotProduct(direcaoParede));

                string titulo = numero + ". " + vao.Categoria
                    + " - a " + distanciaCm.ToString("F0", AjustarVaosWindow.CulturaBr) + " cm do inicio da parede"
                    + " (Id " + vao.ElementoId.IntegerValue + ")";

                _vaosParaAjuste.Add(new VaoParaAjuste(vao, titulo));
                numero++;
            }

            PanelVaos.Visibility = Visibility.Visible;
            AtualizarResumoVaos();
        }

        private void AtualizarResumoVaos()
        {
            int total = _vaosParaAjuste.Count;
            int ajustados = _vaosParaAjuste.Count(v => v.EstaAjustado());

            string detectados = total + (total == 1 ? " vao detectado" : " vaos detectados");
            string situacao = ajustados == 0
                ? "medidas automaticas"
                : ajustados + " com medidas ajustadas";

            TextResumoVaos.Text = detectados + " - " + situacao;
        }

        private void BtnAjustarVaos_Click(object sender, RoutedEventArgs e)
        {
            var janelaVaos = new AjustarVaosWindow(_vaosParaAjuste, _alturaParedeCm)
            {
                Owner = this
            };

            janelaVaos.ShowDialog();
            AtualizarResumoVaos();
        }

        // ------------------------------------------------------------------

        private void BtnGerar_Click(object sender, RoutedEventArgs e)
        {
            bool ehDrywall = RadioSistemaDrywall.IsChecked == true;

            double larguraMm;
            double bitolaMm;

            if (ehDrywall)
            {
                larguraMm = RadioLargura70DW.IsChecked == true ? 70
                    : RadioLargura90DW.IsChecked == true ? 90
                    : 48;
                bitolaMm = PerfilSpec.BitolaDrywallMm;
            }
            else
            {
                larguraMm = RadioLargura140SF.IsChecked == true ? 140
                    : RadioLargura200SF.IsChecked == true ? 200
                    : 90;
                bitolaMm = RadioBitola080.IsChecked == true ? 0.80
                    : RadioBitola125.IsChecked == true ? 1.25
                    : 0.95;
            }

            PerfilEscolhido = new PerfilSpec
            {
                Sistema = ehDrywall ? SistemaPerfil.Drywall : SistemaPerfil.SteelFrame,
                LarguraAlmaMm = larguraMm,
                EspessuraChapaMm = bitolaMm
            };

            EspacamentoEscolhidoCm = RadioBtn60.IsChecked == true ? 60.0 : 40.0;

            PreferenciasGeracao.Salvar(PerfilEscolhido, EspacamentoEscolhidoCm);

            QuantidadeVaosAjustados = _vaosParaAjuste.Count(v => v.EstaAjustado());

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