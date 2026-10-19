using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Janela de configuracao explicita da laje: sistema, tipo, perfil, orientacao das vigas,
    /// espacamento, cota e espessura da telha. Mostra uma PREVIA (mesmo calculo da geracao, sem
    /// criar geometria) para o usuario conferir antes de gerar. So os sistemas e tipos
    /// realmente implementados sao oferecidos.
    /// </summary>
    public partial class LajeConfigWindow : Window
    {
        private sealed class ItemSistema
        {
            public DescricaoSistemaLaje Descricao;
            public override string ToString() { return Descricao.Nome; }
        }

        private sealed class ItemOrientacao
        {
            public OrientacaoUI Ui;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemTipoViga
        {
            public TipoVigaLaje Tipo;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemComposicao
        {
            public ComposicaoTipo Tipo;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemTrelica
        {
            public TrelicaTipo Tipo;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemMontagemTrelica
        {
            public MontagemTrelica Tipo;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemComposicaoCaixao
        {
            public ComposicaoCaixaoTipo Tipo;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private sealed class ItemFixador
        {
            public NivelDetalheFixadores Nivel;
            public string Nome;
            public override string ToString() { return Nome; }
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly ContornoLaje _contorno;
        private readonly CatalogoLaje _catalogo;
        private readonly Func<OrientacaoVigas, IEnumerable<double>> _posicoesApoioProvider;
        private readonly double _toleranciaAlinhamentoMm;

        /// <summary>Parametros escolhidos. So valido se DialogResult for true.</summary>
        public ParametrosLaje Resultado { get; private set; }

        /// <summary>Previa final mostrada/confirmada. So valida se DialogResult for true.</summary>
        public PreviaLaje Previa { get; private set; }

        /// <param name="contorno">Contorno ja extraido da origem (piso/bordas/pontos).</param>
        /// <param name="origem">De onde veio o contorno (para exibir).</param>
        /// <param name="cotaSugeridaMm">Cota Z sugerida como topo do conjunto acabado.</param>
        /// <param name="catalogo">Catalogo da laje.</param>
        /// <param name="existente">Configuracao anterior, para edicao/regeneracao; nulo na primeira vez.</param>
        public LajeConfigWindow(
            ContornoLaje contorno,
            TipoOrigemContorno origem,
            double cotaSugeridaMm,
            CatalogoLaje catalogo,
            ParametrosLaje existente = null,
            Func<OrientacaoVigas, IEnumerable<double>> posicoesApoioProvider = null,
            double toleranciaAlinhamentoMm = 0.0)
        {
            if (contorno == null) throw new ArgumentNullException(nameof(contorno));

            InitializeComponent();

            _contorno = contorno;
            _catalogo = catalogo ?? CatalogoLaje.CriarPadrao();
            _posicoesApoioProvider = posicoesApoioProvider;
            _toleranciaAlinhamentoMm = toleranciaAlinhamentoMm;

            PopularSistemas();
            PopularOrientacoes();
            PopularTiposViga();
            PopularTrelicas();
            PopularMontagemTrelica();
            PopularComposicaoCaixao();
            PopularFixadores();
            PopularComposicoes();
            TextOrigem.Text = "Origem do contorno: " + TipoOrigemContornoTexto.Nome(origem)
                + " - area " + contorno.AreaM2.ToString("F2", Inv) + " m2";

            PopularTelhas();
            Carregar(existente, cotaSugeridaMm);
            AtualizarPaineis();
            AtualizarPrevia();
        }

        private void PopularSistemas()
        {
            foreach (DescricaoSistemaLaje d in CatalogoSistemasLaje.Todos)
            {
                if (!d.Implementado) continue;
                ComboSistema.Items.Add(new ItemSistema { Descricao = d });
            }

            if (ComboSistema.Items.Count > 0) ComboSistema.SelectedIndex = 0;
        }

        private void PopularOrientacoes()
        {
            ComboOrientacao.Items.Add(new ItemOrientacao
            {
                Ui = OrientacaoUI.Automatica,
                Nome = "Automatica (lado mais curto)"
            });
            ComboOrientacao.Items.Add(new ItemOrientacao
            {
                Ui = OrientacaoUI.PorBordaReferencia,
                Nome = "Por borda de referencia"
            });
            ComboOrientacao.Items.Add(new ItemOrientacao
            {
                Ui = OrientacaoUI.Manual,
                Nome = "Manual (graus)"
            });
            ComboOrientacao.SelectedIndex = 0;
        }

        private void PopularTiposViga()
        {
            if (ComboTipoViga == null) return;
            ComboTipoViga.Items.Clear();
            foreach (CatalogoTiposVigaLaje.TipoVigaInfo info in CatalogoTiposVigaLaje.Todos)
            {
                if (!info.Implementado) continue;
                ComboTipoViga.Items.Add(new ItemTipoViga { Tipo = info.Tipo, Nome = info.Descricao });
            }
            if (ComboTipoViga.Items.Count > 0) ComboTipoViga.SelectedIndex = 0;
        }

        private void PopularTrelicas()
        {
            if (ComboTrelicaTipo == null) return;
            ComboTrelicaTipo.Items.Clear();
            foreach (CatalogoTrelica.TrelicaInfo info in CatalogoTrelica.Todos)
            {
                if (!info.Implementado) continue;
                ComboTrelicaTipo.Items.Add(new ItemTrelica { Tipo = info.Tipo, Nome = info.Nome });
            }
            if (ComboTrelicaTipo.Items.Count > 0) ComboTrelicaTipo.SelectedIndex = 0;
        }

        private void PopularMontagemTrelica()
        {
            if (ComboMontagemTrelica == null) return;
            ComboMontagemTrelica.Items.Clear();
            ComboMontagemTrelica.Items.Add(new ItemMontagemTrelica
            {
                Tipo = MontagemTrelica.Encaixada,
                Nome = "Encaixada (pecas unicas)"
            });
            ComboMontagemTrelica.Items.Add(new ItemMontagemTrelica
            {
                Tipo = MontagemTrelica.BackToBack,
                Nome = "Back to Back (pecas duplas)"
            });
            ComboMontagemTrelica.SelectedIndex = 0;
        }

        private void PopularComposicaoCaixao()
        {
            if (ComboComposicaoCaixao == null) return;
            ComboComposicaoCaixao.Items.Clear();
            ComboComposicaoCaixao.Items.Add(new ItemComposicaoCaixao
            {
                Tipo = ComposicaoCaixaoTipo.FrenteAFrente,
                Nome = "Frente a frente (aberturas opostas)"
            });
            ComboComposicaoCaixao.Items.Add(new ItemComposicaoCaixao
            {
                Tipo = ComposicaoCaixaoTipo.Encaixados,
                Nome = "Encaixados (almas separadas)"
            });
            ComboComposicaoCaixao.SelectedIndex = 0;
        }

        private void PopularFixadores()
        {
            if (ComboFixadores == null) return;
            ComboFixadores.Items.Clear();
            ComboFixadores.Items.Add(new ItemFixador { Nivel = NivelDetalheFixadores.Oculto, Nome = "Oculto (nao gerar)" });
            ComboFixadores.Items.Add(new ItemFixador { Nivel = NivelDetalheFixadores.Simplificado, Nome = "Simplificado" });
            ComboFixadores.Items.Add(new ItemFixador { Nivel = NivelDetalheFixadores.Detalhado, Nome = "Detalhado" });
            ComboFixadores.SelectedIndex = 0;
        }

        private void PopularComposicoes()
        {
            if (ComboComposicao == null) return;
            ComboComposicao.Items.Clear();
            ComboComposicao.Items.Add(new ItemComposicao { Tipo = ComposicaoTipo.Nenhuma, Nome = "Nenhuma (padrao do catalogo)" });
            ComboComposicao.Items.Add(new ItemComposicao { Tipo = ComposicaoTipo.OSB, Nome = "OSB" });
            ComboComposicao.Items.Add(new ItemComposicao { Tipo = ComposicaoTipo.Cimenticia, Nome = "Placa cimenticia" });
            ComboComposicao.Items.Add(new ItemComposicao { Tipo = ComposicaoTipo.OSBMaisCimenticia, Nome = "OSB + cimenticia 10 mm" });
            ComboComposicao.SelectedIndex = 0;

            ComboOSB.Items.Clear();
            foreach (double e in ComposicaoCobertura.EspessurasOSBMm)
            {
                ComboOSB.Items.Add(e.ToString("F0", Inv));
            }
            ComboOSB.SelectedIndex = 0;

            ComboCimenticia.Items.Clear();
            foreach (double e in ComposicaoCobertura.EspessurasCimenticiaMm)
            {
                ComboCimenticia.Items.Add(e.ToString("F0", Inv));
            }
            ComboCimenticia.SelectedIndex = 0;
        }

        private void ComboComposicao_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void SelecionarTipoViga(TipoVigaLaje tipo)
        {
            if (ComboTipoViga == null) return;
            foreach (object item in ComboTipoViga.Items)
            {
                if (((ItemTipoViga)item).Tipo == tipo)
                {
                    ComboTipoViga.SelectedItem = item;
                    return;
                }
            }
            if (ComboTipoViga.Items.Count > 0) ComboTipoViga.SelectedIndex = 0;
        }

        private TipoVigaLaje LerTipoViga()
        {
            ItemTipoViga item = ComboTipoViga?.SelectedItem as ItemTipoViga;
            return item?.Tipo ?? TipoVigaLaje.PerfilSimples;
        }

        private void SelecionarTrelica(TrelicaTipo tipo, double alturaMm, int? paineisMm, double painelAlvoMm)
        {
            if (ComboTrelicaTipo != null)
            {
                foreach (object item in ComboTrelicaTipo.Items)
                {
                    if (((ItemTrelica)item).Tipo == tipo)
                    {
                        ComboTrelicaTipo.SelectedItem = item;
                        break;
                    }
                }
            }

            TextAlturaTrelica.Text = alturaMm.ToString("F0", Inv);
            TextPaineisTrelica.Text = paineisMm.HasValue
                ? paineisMm.Value.ToString(Inv)
                : string.Empty;
        }

        private TrelicaTipo LerTipoTrelica()
        {
            ItemTrelica item = ComboTrelicaTipo?.SelectedItem as ItemTrelica;
            return item?.Tipo ?? TrelicaTipo.Warren;
        }

        private void SelecionarMontagemTrelica(MontagemTrelica montagem)
        {
            if (ComboMontagemTrelica == null) return;
            foreach (object item in ComboMontagemTrelica.Items)
            {
                if (((ItemMontagemTrelica)item).Tipo == montagem)
                {
                    ComboMontagemTrelica.SelectedItem = item;
                    break;
                }
            }
        }

        private MontagemTrelica LerMontagemTrelica()
        {
            ItemMontagemTrelica item = ComboMontagemTrelica?.SelectedItem as ItemMontagemTrelica;
            return item?.Tipo ?? MontagemTrelica.Encaixada;
        }

        private void SelecionarComposicaoCaixao(ComposicaoCaixaoTipo tipo)
        {
            if (ComboComposicaoCaixao == null) return;
            foreach (object item in ComboComposicaoCaixao.Items)
            {
                if (((ItemComposicaoCaixao)item).Tipo == tipo)
                {
                    ComboComposicaoCaixao.SelectedItem = item;
                    break;
                }
            }
        }

        private ComposicaoCaixaoTipo LerComposicaoCaixao()
        {
            ItemComposicaoCaixao item = ComboComposicaoCaixao?.SelectedItem as ItemComposicaoCaixao;
            return item?.Tipo ?? ComposicaoCaixaoTipo.FrenteAFrente;
        }

        private void ComboTrelicaTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarEsquemaTrelica();
        }

        /// <summary>
        /// Desenha um esquema vetorial da trelica (banzos, montantes e diagonais) no Canvas, usando
        /// a MESMA geometria pura do dominio (ComposicaoTrelica.Membros). E apenas um auxilio de
        /// escolha: nao altera a geometria de geracao. O desenho muda conforme o tipo selecionado.
        /// </summary>
        private void AtualizarEsquemaTrelica()
        {
            if (CanvasTrelica == null) return;

            CanvasTrelica.Children.Clear();

            TrelicaTipo tipo = LerTipoTrelica();
            const double vaoEsqMm = 360.0;
            const int paineisEsq = 6;

            double alturaEsqMm = ComposicaoTrelica.AlturaPadraoMm;
            double lido;
            if (TentarLerNumero(TextAlturaTrelica, out lido) && lido >= 40.0 && lido <= 1000.0)
                alturaEsqMm = lido;

            List<MembroTrelica> membros;
            try
            {
                membros = ComposicaoTrelica.Membros(tipo, vaoEsqMm, alturaEsqMm, paineisEsq);
            }
            catch
            {
                return;
            }

            if (membros.Count == 0) return;

            double largura = CanvasTrelica.Width;
            double altura = CanvasTrelica.Height;
            const double margem = 4.0;

            double meiaAltura = alturaEsqMm / 2.0;
            double escala = System.Math.Min(
                (largura - 2.0 * margem) / vaoEsqMm,
                (altura - 2.0 * margem) / alturaEsqMm);
            if (escala <= 0.0) return;

            double offsetX = (largura - vaoEsqMm * escala) / 2.0;
            double offsetY = (altura - alturaEsqMm * escala) / 2.0;

            var brushBanzo = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            var brushDiagonal = new SolidColorBrush(Color.FromRgb(30, 90, 180));
            var brushMontante = new SolidColorBrush(Color.FromRgb(140, 140, 140));

            foreach (MembroTrelica m in membros)
            {
                double x1 = offsetX + m.InicioUMm * escala;
                double y1 = offsetY + (meiaAltura - m.InicioZMm) * escala;
                double x2 = offsetX + m.FimUMm * escala;
                double y2 = offsetY + (meiaAltura - m.FimZMm) * escala;

                bool banzo = m.Tipo == MembroTrelicaTipo.BanzoSuperior
                    || m.Tipo == MembroTrelicaTipo.BanzoInferior;

                var linha = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = banzo ? brushBanzo : (m.Tipo == MembroTrelicaTipo.Diagonal ? brushDiagonal : brushMontante),
                    StrokeThickness = banzo ? 2.5 : 1.4,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                CanvasTrelica.Children.Add(linha);
            }
        }

        private void ComboTipoViga_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void CheckFitasTensionadas_Changed(object sender, RoutedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void SelecionarFixador(NivelDetalheFixadores nivel, double espacamentoMm)
        {
            if (ComboFixadores != null)
            {
                foreach (object item in ComboFixadores.Items)
                {
                    if (((ItemFixador)item).Nivel == nivel)
                    {
                        ComboFixadores.SelectedItem = item;
                        break;
                    }
                }
            }

            if (TextEspacamentoFixadores != null)
                TextEspacamentoFixadores.Text = espacamentoMm.ToString("F0", Inv);
        }

        private NivelDetalheFixadores LerNivelFixador()
        {
            ItemFixador item = ComboFixadores?.SelectedItem as ItemFixador;
            return item?.Nivel ?? NivelDetalheFixadores.Oculto;
        }

        private void ComboFixadores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void SelecionarComposicao(ComposicaoTipo tipo, double espessuraOSBMm, double espessuraCimenticiaMm)
        {
            if (ComboComposicao == null) return;
            foreach (object item in ComboComposicao.Items)
            {
                if (((ItemComposicao)item).Tipo == tipo)
                {
                    ComboComposicao.SelectedItem = item;
                    break;
                }
            }
            SelecionarDouble(ComboOSB, espessuraOSBMm);
            SelecionarDouble(ComboCimenticia, espessuraCimenticiaMm);
        }

        private static void SelecionarDouble(ComboBox combo, double valor)
        {
            if (combo == null) return;
            string alvo = valor.ToString("F0", Inv);
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i]?.ToString(), alvo))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static double LerDouble(ComboBox combo, double padrao)
        {
            if (combo == null || combo.SelectedItem == null) return padrao;
            double v;
            return double.TryParse(combo.SelectedItem.ToString(), NumberStyles.Float, Inv, out v) ? v : padrao;
        }

        private ComposicaoTipo LerComposicaoTipo()
        {
            ItemComposicao item = ComboComposicao?.SelectedItem as ItemComposicao;
            return item?.Tipo ?? ComposicaoTipo.Nenhuma;
        }

        private void PopularTelhas()
        {
            if (ComboTelha == null) return;
            ComboTelha.Items.Clear();
            foreach (double e in TelhaTp40.EspessurasDisponiveisMm)
            {
                ComboTelha.Items.Add(e.ToString("F2", Inv));
            }
            if (ComboTelha.Items.Count > 0) ComboTelha.SelectedIndex = 0;
        }

        private void SelecionarTelha(double espessuraMm)
        {
            if (ComboTelha == null) return;
            string alvo = espessuraMm.ToString("F2", Inv);
            for (int i = 0; i < ComboTelha.Items.Count; i++)
            {
                if (string.Equals(ComboTelha.Items[i]?.ToString(), alvo))
                {
                    ComboTelha.SelectedIndex = i;
                    return;
                }
            }
            if (ComboTelha.Items.Count > 0) ComboTelha.SelectedIndex = 0;
        }

        private double LerTelha()
        {
            if (ComboTelha == null || ComboTelha.SelectedItem == null) return TelhaTp40.Espessura043Mm;
            string s = ComboTelha.SelectedItem.ToString();
            double v;
            double.TryParse(s, NumberStyles.Float, Inv, out v);
            return v;
        }

        private void Carregar(ParametrosLaje existente, double cotaSugeridaMm)
        {
            if (existente != null)
            {
                RadioTipoUmida.IsChecked = existente.Tipo == TipoLaje.Umida;
                RadioTipoSeca.IsChecked = existente.Tipo == TipoLaje.Seca;

                PerfilSpec perfil = existente.Perfil ?? PerfilSpec.CriarPadrao();
                if (perfil.Sistema == SistemaPerfil.Drywall)
                {
                    // Drywall nao e mais oferecido para lajes: cai no padrao Steel Frame do catalogo.
                    perfil = CatalogoPerfisLaje.CriarPadrao();
                }
                SelecionarLargura(perfil);
                SelecionarBitola(perfil);

                SelecionarTelha(existente.EspessuraTelhaMm);
                TextEspacamento.Text = existente.EspacamentoMm.ToString("F0", Inv);
                TextCota.Text = existente.CotaMm.ToString("F0", Inv);
                TextAresta.Text = existente.ArestaReferencia.ToString(Inv);
                TextGraus.Text = existente.DirecaoVigasGraus.HasValue
                    ? existente.DirecaoVigasGraus.Value.ToString("F1", Inv)
                    : string.Empty;

                SelecionarOrientacao(existente.ModoOrientacao);
                if (CheckPerpendicular != null)
                    CheckPerpendicular.IsChecked = existente.ModoOrientacao == OrientacaoVigasModo.PerpendicularAresta;

                SelecionarTipoViga(existente.TipoViga);
                SelecionarTrelica(
                    existente.Trelica, existente.AlturaTrelicaMm,
                    existente.NumeroPaineisTrelica, existente.PainelTrelicaAlvoMm);
                SelecionarMontagemTrelica(existente.MontagemTrelica);
                if (CheckGussetPlacas != null) CheckGussetPlacas.IsChecked = existente.GussetPlacas;
                if (CheckFitasTensionadas != null) CheckFitasTensionadas.IsChecked = existente.FitasTensionadas;
                if (TextEspacamentoFitas != null)
                    TextEspacamentoFitas.Text = existente.EspacamentoFitasTensionadasMm.ToString("F0", Inv);
                SelecionarComposicaoCaixao(existente.ComposicaoCaixao);
                SelecionarFixador(existente.NivelFixadores, existente.EspacamentoFixadoresPlacaMm);
                SelecionarComposicao(
                    existente.ComposicaoPlacas, existente.EspessuraOSBMm, existente.EspessuraCimenticiaMm);
                if (CheckPreencherOndas != null) CheckPreencherOndas.IsChecked = existente.PreencherOndas;
            }
            else
            {
                RadioTipoUmida.IsChecked = true;

                var (perfil, espacamentoCm) = PreferenciasGeracao.CarregarUltimaSelecaoOuPadrao();
                if (perfil.Sistema == SistemaPerfil.Drywall) perfil = CatalogoPerfisLaje.CriarPadrao();
                SelecionarLargura(perfil);
                SelecionarBitola(perfil);

                SelecionarTelha(TelhaTp40.Espessura043Mm);
                TextEspacamento.Text = (espacamentoCm * 10.0).ToString("F0", Inv);
                TextCota.Text = cotaSugeridaMm.ToString("F0", Inv);
                TextAresta.Text = "0";
                ComboOrientacao.SelectedIndex = 0;
                SelecionarTipoViga(TipoVigaLaje.PerfilSimples);
                SelecionarTrelica(
                    TrelicaTipo.Warren, ComposicaoTrelica.AlturaPadraoMm, null, ComposicaoTrelica.PainelAlvoPadraoMm);
                SelecionarMontagemTrelica(MontagemTrelica.Encaixada);
                if (CheckGussetPlacas != null) CheckGussetPlacas.IsChecked = false;
                if (CheckFitasTensionadas != null) CheckFitasTensionadas.IsChecked = false;
                if (TextEspacamentoFitas != null)
                    TextEspacamentoFitas.Text = FitasTensionadasLaje.PassoPadraoMm.ToString("F0", Inv);
                SelecionarComposicaoCaixao(ComposicaoCaixaoTipo.FrenteAFrente);
                SelecionarFixador(NivelDetalheFixadores.Oculto, FixadoresLaje.EspacamentoPlacaPadraoMm);
                SelecionarComposicao(ComposicaoTipo.Nenhuma, 15.0, 10.0);
                if (CheckPreencherOndas != null) CheckPreencherOndas.IsChecked = true;
            }
        }

        private void SelecionarLargura(PerfilSpec perfil)
        {
            foreach (object filho in PanelLarguraSteelFrame.Children)
            {
                if (filho is RadioButton radio)
                {
                    radio.IsChecked = radio.Content?.ToString() == perfil.LarguraAlmaMm.ToString("F0", Inv) + " mm";
                }
            }
        }

        private void SelecionarBitola(PerfilSpec perfil)
        {
            bool b125 = Math.Abs(perfil.EspessuraChapaMm - 1.25) < 1e-6;
            bool b095 = Math.Abs(perfil.EspessuraChapaMm - 0.95) < 1e-6;
            if (!b125 && !b095)
            {
                // Bitola fora do catalogo de laje (ex.: 0,80 de config antiga): assume 0,95.
                perfil.EspessuraChapaMm = 0.95;
                b095 = true;
            }

            RadioBitola125.IsChecked = b125;
            RadioBitola095.IsChecked = b095;
        }

        private void RadioTipo_Checked(object sender, RoutedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void SelecionarOrientacao(OrientacaoVigasModo modo)
        {
            OrientacaoUI ui = OrientacaoHelper.MapearParaUI(modo);
            foreach (object item in ComboOrientacao.Items)
            {
                if (((ItemOrientacao)item).Ui == ui)
                {
                    ComboOrientacao.SelectedItem = item;
                    break;
                }
            }
            if (CheckPerpendicular != null)
                CheckPerpendicular.IsChecked = modo == OrientacaoVigasModo.PerpendicularAresta;
        }

        private void ComboOrientacao_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void CheckPerpendicular_Checked(object sender, RoutedEventArgs e)
        {
            AtualizarPaineis();
        }

        private void AtualizarPaineis()
        {
            if (PanelTelha == null) return;

            bool umida = RadioTipoSeca == null || RadioTipoSeca.IsChecked != true;
            PanelTelha.Visibility = umida ? Visibility.Visible : Visibility.Collapsed;
            if (PanelPreencherOndas != null)
                PanelPreencherOndas.Visibility = umida ? Visibility.Visible : Visibility.Collapsed;
            if (PanelComposicao != null)
                PanelComposicao.Visibility = umida ? Visibility.Collapsed : Visibility.Visible;

            ComposicaoTipo composicao = LerComposicaoTipo();
            if (PanelOSB != null)
                PanelOSB.Visibility = !umida && ComposicaoCobertura.UsaOSB(composicao)
                    ? Visibility.Visible : Visibility.Collapsed;
            if (PanelCimenticia != null)
                PanelCimenticia.Visibility = !umida && ComposicaoCobertura.UsaCimenticia(composicao)
                    ? Visibility.Visible : Visibility.Collapsed;

            if (PanelTrelica != null)
                PanelTrelica.Visibility = LerTipoViga() == TipoVigaLaje.Trelica
                    ? Visibility.Visible : Visibility.Collapsed;

            if (PanelFitas != null)
                PanelFitas.Visibility = LerTipoViga() == TipoVigaLaje.Trelica
                    && CheckFitasTensionadas != null && CheckFitasTensionadas.IsChecked == true
                    ? Visibility.Visible : Visibility.Collapsed;

            if (PanelCaixao != null)
                PanelCaixao.Visibility = LerTipoViga() == TipoVigaLaje.Caixao
                    ? Visibility.Visible : Visibility.Collapsed;

            if (PanelEspacamentoFixadores != null)
                PanelEspacamentoFixadores.Visibility = LerNivelFixador() != NivelDetalheFixadores.Oculto
                    ? Visibility.Visible : Visibility.Collapsed;

            if (!AlgumSelecionado(PanelLarguraSteelFrame)) RadioLargura90SF.IsChecked = true;
            if (!AlgumSelecionado(PanelBitola)) RadioBitola095.IsChecked = true;

            AtualizarEsquemaTrelica();

            OrientacaoVigasModo modo = ModoSelecionado();
            bool porAresta = OrientacaoHelper.EhPorBorda(modo);

            LabelAresta.Visibility = porAresta ? Visibility.Visible : Visibility.Collapsed;
            TextAresta.Visibility = porAresta ? Visibility.Visible : Visibility.Collapsed;
            LabelGraus.Visibility = modo == OrientacaoVigasModo.Manual ? Visibility.Visible : Visibility.Collapsed;
            TextGraus.Visibility = modo == OrientacaoVigasModo.Manual ? Visibility.Visible : Visibility.Collapsed;
            if (CheckPerpendicular != null)
                CheckPerpendicular.Visibility = porAresta ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool AlgumSelecionado(StackPanel painel)
        {
            foreach (object filho in painel.Children)
            {
                if (filho is RadioButton radio && radio.IsChecked == true) return true;
            }

            return false;
        }

        private OrientacaoVigasModo ModoSelecionado()
        {
            ItemOrientacao item = ComboOrientacao.SelectedItem as ItemOrientacao;
            OrientacaoUI ui = item?.Ui ?? OrientacaoUI.Automatica;
            bool perpendicular = CheckPerpendicular != null && CheckPerpendicular.IsChecked == true;
            return OrientacaoHelper.MapearParaModo(ui, perpendicular);
        }

        private static bool TentarLerNumero(TextBox caixa, out double valor)
        {
            string texto = (caixa.Text ?? string.Empty).Trim();
            return double.TryParse(texto, NumberStyles.Float, CultureInfo.CurrentCulture, out valor)
                || double.TryParse(texto, NumberStyles.Float, Inv, out valor);
        }

        /// <summary>
        /// Monta os parametros a partir dos controles. Devolve null e uma mensagem quando algum
        /// campo nao pode ser lido.
        /// </summary>
        private ParametrosLaje LerParametros(out string erro)
        {
            erro = null;

            double espacamento;
            if (!TentarLerNumero(TextEspacamento, out espacamento))
            {
                erro = "Espacamento invalido. Informe um numero em milimetros.";
                return null;
            }

            double cota;
            if (!TentarLerNumero(TextCota, out cota))
            {
                erro = "Cota invalida. Informe um numero em milimetros.";
                return null;
            }

            TipoLaje tipo = RadioTipoSeca.IsChecked == true ? TipoLaje.Seca : TipoLaje.Umida;

            double telha = TelhaTp40.Espessura043Mm;
            if (tipo == TipoLaje.Umida)
            {
                telha = LerTelha();
            }

            OrientacaoVigasModo modo = ModoSelecionado();
            double? graus = null;
            if (modo == OrientacaoVigasModo.Manual)
            {
                double g;
                if (!TentarLerNumero(TextGraus, out g))
                {
                    erro = "Direcao manual invalida. Informe o angulo em graus.";
                    return null;
                }

                graus = g;
            }

            ItemSistema sistema = ComboSistema.SelectedItem as ItemSistema;

            TipoVigaLaje tipoVigaLaje = LerTipoViga();
            TrelicaTipo trelicaTipo = TrelicaTipo.Warren;
            double alturaTrelica = ComposicaoTrelica.AlturaPadraoMm;
            int? paineisTrelica = null;

            if (tipoVigaLaje == TipoVigaLaje.Trelica)
            {
                trelicaTipo = LerTipoTrelica();

                if (!TentarLerNumero(TextAlturaTrelica, out alturaTrelica))
                {
                    erro = "Altura da trelica invalida. Informe um numero em milimetros.";
                    return null;
                }

                string paineisTexto = (TextPaineisTrelica.Text ?? string.Empty).Trim();
                if (paineisTexto.Length > 0)
                {
                    int paineis;
                    if (!int.TryParse(paineisTexto, NumberStyles.Integer, Inv, out paineis))
                    {
                        erro = "Numero de paineis da trelica invalido. Deixe vazio para automatico.";
                        return null;
                    }

                    paineisTrelica = paineis;
                }
            }

            bool fitasTensionadas = CheckFitasTensionadas != null && CheckFitasTensionadas.IsChecked == true;
            double espacamentoFitas = FitasTensionadasLaje.PassoPadraoMm;
            if (fitasTensionadas && tipoVigaLaje == TipoVigaLaje.Trelica
                && !TentarLerNumero(TextEspacamentoFitas, out espacamentoFitas))
            {
                erro = "Passo das fitas tensionadas invalido. Informe um numero em milimetros.";
                return null;
            }

            NivelDetalheFixadores nivelFixadores = LerNivelFixador();
            double espacamentoFixadores = FixadoresLaje.EspacamentoPlacaPadraoMm;
            if (nivelFixadores != NivelDetalheFixadores.Oculto)
            {
                if (!TentarLerNumero(TextEspacamentoFixadores, out espacamentoFixadores))
                {
                    erro = "Espacamento dos fixadores invalido. Informe um numero em milimetros.";
                    return null;
                }
            }

            return new ParametrosLaje
            {
                Contorno = _contorno,
                Sistema = sistema != null ? sistema.Descricao.Sistema : SistemaLaje.VigasLsfComGuiasBorda,
                Tipo = tipo,
                TipoViga = tipoVigaLaje,
                Trelica = trelicaTipo,
                MontagemTrelica = LerMontagemTrelica(),
                GussetPlacas = CheckGussetPlacas != null && CheckGussetPlacas.IsChecked == true,
                FitasTensionadas = fitasTensionadas,
                EspacamentoFitasTensionadasMm = espacamentoFitas,
                ComposicaoCaixao = LerComposicaoCaixao(),
                AlturaTrelicaMm = alturaTrelica,
                NumeroPaineisTrelica = paineisTrelica,
                NivelFixadores = nivelFixadores,
                EspacamentoFixadoresPlacaMm = espacamentoFixadores,
                Perfil = LerPerfil(),
                EspacamentoMm = espacamento,
                CotaMm = cota,
                EspessuraTelhaMm = telha,
                ComposicaoPlacas = tipo == TipoLaje.Seca ? LerComposicaoTipo() : ComposicaoTipo.Nenhuma,
                EspessuraOSBMm = LerDouble(ComboOSB, 15.0),
                EspessuraCimenticiaMm = LerDouble(ComboCimenticia, 10.0),
                PreencherOndas = CheckPreencherOndas == null || CheckPreencherOndas.IsChecked == true,
                ModoOrientacao = modo,
                ArestaReferencia = OrientacaoHelper.EhPorBorda(modo)
                        ? LerAresta()
                        : 0,
                DirecaoVigasGraus = graus
            };
        }

        private int LerAresta()
        {
            int valor;
            if (int.TryParse((TextAresta.Text ?? string.Empty).Trim(), NumberStyles.Integer, Inv, out valor))
            {
                return valor;
            }

            return 0;
        }

        private PerfilSpec LerPerfil()
        {
            double larguraSf = RadioLargura140SF.IsChecked == true ? 140.0
                : RadioLargura200SF.IsChecked == true ? 200.0
                : 90.0;

            double bitolaSf = RadioBitola125.IsChecked == true ? 1.25 : 0.95;

            return new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = larguraSf,
                EspessuraChapaMm = bitolaSf
            };
        }

        private void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            AtualizarPrevia();
        }

        private void AtualizarPrevia()
        {
            if (TextPrevia == null) return;

            string erro;
            ParametrosLaje parametros = LerParametros(out erro);
            if (parametros == null)
            {
                TextPrevia.Text = "Erro: " + erro;
                return;
            }

            PreviaLaje previa = PreviaLaje.Calcular(
                parametros, _catalogo, _posicoesApoioProvider, _toleranciaAlinhamentoMm);
            TextPrevia.Text = string.Join(Environment.NewLine, previa.Linhas());
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string erro;
            ParametrosLaje parametros = LerParametros(out erro);
            if (parametros == null)
            {
                MessageBox.Show(this, erro, "LSF Modeler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PreviaLaje previa = PreviaLaje.Calcular(
                parametros, _catalogo, _posicoesApoioProvider, _toleranciaAlinhamentoMm);
            if (!previa.EhValido)
            {
                TextPrevia.Text = string.Join(Environment.NewLine, previa.Linhas());
                MessageBox.Show(
                    this,
                    "A previa tem erros que impedem a geracao. Veja o quadro da previa.",
                    "LSF Modeler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Resultado = parametros;
            Previa = previa;
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
