using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Previa pura de uma laje: roda orientacao, layout e camadas sem tocar no Revit, para a
    /// interface mostrar o resumo ANTES de gerar e para o usuario confirmar. Nao cria geometria;
    /// e exatamente o mesmo calculo que a geracao usa, entao o que aparece na previa e o que sera
    /// gerado. Reune tambem avisos (nao impedem) e erros (impedem).
    /// </summary>
    public sealed class PreviaLaje
    {
        private readonly List<CamadaLaje> _camadas = new List<CamadaLaje>();

        private PreviaLaje()
        {
        }

        public bool EhValido => Erros.Count == 0;

        public double? DirecaoEfetivaGraus { get; private set; }

        public bool DirecaoAutomatica { get; private set; }

        public double AreaM2 { get; private set; }

        public double VaoMaximoMm { get; private set; }

        public double MaiorVaoEntreVigasMm { get; private set; }

        public int QuantidadeVigas { get; private set; }

        public int SegmentosDeViga { get; private set; }

        public int QuantidadeGuiasBorda { get; private set; }

        public double AlturaTotalMm { get; private set; }

        /// <summary>true quando a viga escolhida e trelicada; os campos abaixo descrevem-na.</summary>
        public bool EhTrelica { get; private set; }

        public TrelicaTipo TrelicaTipoSelecionada { get; private set; }

        public double AlturaTrelicaMm { get; private set; }

        public int? NumeroPaineisTrelica { get; private set; }

        public NivelDetalheFixadores NivelFixadores { get; private set; } = NivelDetalheFixadores.Oculto;

        public int FixadoresEstruturais { get; private set; }

        public int FixadoresPlaca { get; private set; }

        /// <summary>Total de chapas de placa (quando o parcelamento esta ligado).</summary>
        public int ChapasPlacas { get; private set; }

        /// <summary>Numero estimado de chapas de telha (laje umida).</summary>
        public int ChapasTelha { get; private set; }

        public IReadOnlyList<CamadaLaje> Camadas => _camadas;

        public List<string> Avisos { get; } = new List<string>();

        public List<string> Erros { get; } = new List<string>();

        /// <summary>
        /// Calcula a previa a partir dos parametros e do catalogo. Nunca lanca por entrada
        /// invalida: os problemas viram Erros/Avisos. Devolve sempre uma instancia.
        /// </summary>
        /// <param name="posicoesApoioProvider">Obtem, para a orientacao calculada, as posicoes v (mm)
        /// dos montantes/apoios para alinhar as vigas INTERNAS; nulo = sem alinhamento. Nunca desloca
        /// vigas de borda (regra do LayoutVigas).</param>
        /// <param name="toleranciaAlinhamentoMm">Tolerancia (mm) do alinhamento viga-montante; 0 = nao alinha.</param>
        public static PreviaLaje Calcular(
            ParametrosLaje parametros,
            CatalogoLaje catalogo,
            Func<OrientacaoVigas, IEnumerable<double>> posicoesApoioProvider = null,
            double toleranciaAlinhamentoMm = 0.0)
        {
            var previa = new PreviaLaje();

            if (parametros == null)
            {
                previa.Erros.Add("Parametros da laje nao informados.");
                return previa;
            }

            if (catalogo == null) catalogo = CatalogoLaje.CriarPadrao();

            List<string> errosParametros = parametros.Validar();
            if (errosParametros.Count > 0)
            {
                previa.Erros.AddRange(errosParametros);
                return previa;
            }

            List<string> errosDirecao;
            double? direcao = ResolvedorOrientacaoVigas.DirecaoGraus(
                parametros.Contorno,
                parametros.ModoOrientacao,
                parametros.ArestaReferencia,
                parametros.DirecaoVigasGraus,
                out errosDirecao);

            if (errosDirecao.Count > 0)
            {
                previa.Erros.AddRange(errosDirecao);
                return previa;
            }

            OrientacaoVigas orientacao;
            ResultadoLayoutVigas layout;
            CamadasLaje camadas;
            try
            {
                orientacao = OrientacaoVigas.Calcular(parametros.Contorno, direcao);

                IReadOnlyList<double> apoios = null;
                if (posicoesApoioProvider != null)
                {
                    List<double> lista = new List<double>();
                    foreach (double v in posicoesApoioProvider(orientacao))
                    {
                        if (!double.IsNaN(v) && !double.IsInfinity(v)) lista.Add(v);
                    }

                    if (lista.Count > 0) apoios = lista;
                    else previa.Avisos.Add(
                        "Alinhamento viga-montante: nenhum montante de apoio encontrado; as vigas " +
                        "internas permanecem na grade de espacamento.");
                }

                layout = LayoutVigas.Calcular(
                    parametros.Contorno,
                    orientacao,
                    parametros.EspacamentoMm,
                    posicoesApoioMm: apoios,
                    toleranciaAlinhamentoMm: apoios != null ? toleranciaAlinhamentoMm : 0.0,
                    larguraVigaMm: ComposicaoViga.LarguraTransversalMm(
                        parametros.TipoViga, parametros.Perfil, parametros.MontagemTrelica,
                        parametros.ComposicaoCaixao));
                camadas = CamadasLaje.Calcular(parametros, catalogo);
            }
            catch (Exception ex)
            {
                previa.Erros.Add("Calculo da laje: " + ex.Message);
                return previa;
            }

            previa.DirecaoEfetivaGraus = direcao;
            previa.DirecaoAutomatica = !direcao.HasValue;
            previa.Avisos.AddRange(layout.Avisos);

            if (!layout.EhValido)
            {
                previa.Erros.AddRange(layout.Erros);
                return previa;
            }

            previa.AreaM2 = parametros.Contorno.AreaM2;
            previa.VaoMaximoMm = orientacao.VaoMaximoMm;
            previa.MaiorVaoEntreVigasMm = layout.MaiorVaoEntreVigasMm;
            previa.QuantidadeVigas = layout.Vigas.Count;
            previa.QuantidadeGuiasBorda = layout.GuiasBorda.Count;

            int segmentos = 0;
            foreach (VigaLaje viga in layout.Vigas) segmentos += viga.Segmentos.Count;
            previa.SegmentosDeViga = segmentos;

            previa.AlturaTotalMm = camadas.AlturaTotalMm;
            foreach (CamadaLaje c in camadas.Camadas) previa._camadas.Add(c);

            previa.EhTrelica = parametros.TipoViga == TipoVigaLaje.Trelica;
            if (previa.EhTrelica)
            {
                previa.TrelicaTipoSelecionada = parametros.Trelica;
                previa.AlturaTrelicaMm = parametros.AlturaTrelicaMm;
                previa.NumeroPaineisTrelica = parametros.NumeroPaineisTrelica;
            }

            previa.NivelFixadores = parametros.NivelFixadores;
            if (parametros.NivelFixadores != NivelDetalheFixadores.Oculto)
            {
                double zCentro = (camadas.TopoVigaMm + camadas.BaseVigaMm) / 2.0;
                double zPlaca = zCentro;
                foreach (CamadaLaje c in camadas.Camadas)
                {
                    if (CamadaLaje.EhPlaca(c.Nome)) { zPlaca = (c.TopoMm + c.BaseMm) / 2.0; break; }
                }

                ResultadoFixadores fixadores = FixadoresLaje.Calcular(
                    layout, parametros.NivelFixadores, zCentro, zPlaca, parametros.EspacamentoFixadoresPlacaMm);
                previa.FixadoresEstruturais = fixadores.Estruturais.Count;
                previa.FixadoresPlaca = fixadores.Placa.Count;
            }

            if (parametros.ComposicaoPlacas != ComposicaoTipo.Nenhuma)
            {
                int indice = 0;
                foreach (CamadaLaje camada in camadas.Camadas)
                {
                    if (!CamadaLaje.EhPlaca(camada.Nome)) continue;

                    bool osb = PaginacaoPlacas.EhCamadaOSB(camada.Nome);
                    double largura;
                    double comprimento;
                    PaginacaoPlacas.DimensoesOrientadas(osb, out largura, out comprimento);

                    double offsetCamada = (indice % 2 == 0) ? 0.0 : comprimento / 2.0;
                    double offsetLinha = largura / 2.0;

                    ResultadoPaginacao paginacao = PaginacaoPlacas.Calcular(
                        parametros.Contorno, orientacao, largura, comprimento,
                        camada.BaseMm, camada.EspessuraMm, PaginacaoPlacas.JuntaPadraoMm,
                        offsetCamada, offsetLinha, indice);
                    previa.Avisos.AddRange(paginacao.Avisos);
                    previa.ChapasPlacas += paginacao.Placas.Count;
                    indice++;
                }
            }

            if (parametros.Tipo == TipoLaje.Umida)
            {
                previa.ChapasTelha = SecaoTelhaTp40.ContagemChapas(
                    orientacao.ExtensaoUMm, orientacao.ExtensaoVMm, catalogo.Telha);
            }

            return previa;
        }

        /// <summary>
        /// Resumo de texto, uma linha por item, para a tela de confirmacao. Em portugues e sem
        /// prometer validacao normativa: descreve so o que sera gerado.
        /// </summary>
        public List<string> Linhas()
        {
            var linhas = new List<string>();

            if (Erros.Count > 0)
            {
                foreach (string e in Erros) linhas.Add("Erro: " + e);
                return linhas;
            }

            linhas.Add(Formatar("Area da laje: {0:F2} m2", AreaM2));
            linhas.Add(Formatar(
                "Direcao das vigas: {0}",
                DirecaoAutomatica
                    ? "automatica (lado mais curto)"
                    : string.Format(CultureInfo.InvariantCulture, "{0:F1} graus", DirecaoEfetivaGraus ?? 0.0)));
            linhas.Add(Formatar("Vao maximo (envolvente): {0:F0} mm", VaoMaximoMm));
            linhas.Add(Formatar("Vigas: {0} ({1} segmentos) e {2} guias de borda",
                QuantidadeVigas, SegmentosDeViga, QuantidadeGuiasBorda));
            linhas.Add(Formatar("Altura total da laje (acabada a base da viga): {0:F1} mm", AlturaTotalMm));

            if (EhTrelica)
            {
                CatalogoTrelica.TrelicaInfo info = CatalogoTrelica.Obter(TrelicaTipoSelecionada);
                string nome = info != null ? info.Nome : TrelicaTipoSelecionada.ToString();
                linhas.Add(Formatar(
                    "Trelica: {0} | altura entre banzos {1:F0} mm | paineis: {2}",
                    nome,
                    AlturaTrelicaMm,
                    NumeroPaineisTrelica.HasValue
                        ? NumeroPaineisTrelica.Value.ToString(CultureInfo.InvariantCulture)
                        : "automatico"));
            }

            if (NivelFixadores != NivelDetalheFixadores.Oculto)
            {
                linhas.Add(Formatar(
                    "Fixadores ({0}): {1} estruturais + {2} de placa",
                    NivelFixadores, FixadoresEstruturais, FixadoresPlaca));
            }

            if (ChapasPlacas > 0)
            {
                linhas.Add(Formatar("Placas parceladas em chapas: {0}", ChapasPlacas));
            }

            if (ChapasTelha > 0)
            {
                linhas.Add(Formatar("Chapas de telha (estimado): {0}", ChapasTelha));
            }

            foreach (CamadaLaje c in Camadas)
            {
                linhas.Add(Formatar(" - {0}: {1:F1} mm", c.Nome, c.EspessuraMm));
            }

            foreach (string a in Avisos) linhas.Add("Aviso: " + a);

            return linhas;
        }

        private static string Formatar(string formato, params object[] argumentos)
        {
            return string.Format(CultureInfo.InvariantCulture, formato, argumentos);
        }
    }
}
