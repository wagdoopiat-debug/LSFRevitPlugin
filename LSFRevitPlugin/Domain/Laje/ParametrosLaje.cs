using System;
using System.Collections.Generic;
using System.Globalization;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Parametros de uma laje a gerar. Validar() devolve os erros que impedem a geracao
    /// (lista vazia = valido), incluindo o escopo da v1: somente viga de perfil simples.
    /// </summary>
    public class ParametrosLaje
    {
        /// <summary>Espacamentos de viga mais usados, em mm (o usuario pode informar outro valor).</summary>
        public static readonly double[] EspacamentosPadraoMm = { 400, 600 };

        public TipoLaje Tipo { get; set; } = TipoLaje.Umida;

        /// <summary>Sistema construtivo. So o implementado pode ser gerado (ver CatalogoSistemasLaje).</summary>
        public SistemaLaje Sistema { get; set; } = SistemaLaje.VigasLsfComGuiasBorda;

        public TipoVigaLaje TipoViga { get; set; } = TipoVigaLaje.PerfilSimples;

        /// <summary>Tipo de trelica quando TipoViga = Trelica (ver CatalogoTrelica).</summary>
        public TrelicaTipo Trelica { get; set; } = TrelicaTipo.Warren;

        /// <summary>
        /// Montagem construtiva das pecas da trelica (Encaixada ou BackToBack) quando TipoViga =
        /// Trelica. Independente do tipo de trelica escolhido.
        /// </summary>
        public MontagemTrelica MontagemTrelica { get; set; } = MontagemTrelica.Encaixada;

        /// <summary>
        /// Indica se deve gerar chapas de gusset (ligacoes) nas juntas da trelica. Opcional,
        /// por padrao desativado. Valores geometricos de partida.
        /// </summary>
        public bool GussetPlacas { get; set; } = false;

        /// <summary>
        /// Configuracao transversal do caixao (frente a frente ou encaixados) quando TipoViga =
        /// Caixao.
        /// </summary>
        public ComposicaoCaixaoTipo ComposicaoCaixao { get; set; } = ComposicaoCaixaoTipo.FrenteAFrente;

        /// <summary>Altura entre eixos dos banzos da trelica (mm) -- parametro geometrico de partida.</summary>
        public double AlturaTrelicaMm { get; set; } = ComposicaoTrelica.AlturaPadraoMm;

        /// <summary>Numero de paineis da trelica; nulo = derivado do vao e de PainelTrelicaAlvoMm.</summary>
        public int? NumeroPaineisTrelica { get; set; }

        /// <summary>Comprimento alvo de painel da trelica (mm) quando o numero de paineis nao e informado.</summary>
        public double PainelTrelicaAlvoMm { get; set; } = ComposicaoTrelica.PainelAlvoPadraoMm;

        public PerfilSpec Perfil { get; set; } = PerfilSpec.CriarPadrao();

        public double EspacamentoMm { get; set; } = 400.0;

        /// <summary>Direcao das vigas em graus; nulo = automatica (lado mais curto).</summary>
        public double? DirecaoVigasGraus { get; set; }

        /// <summary>Como a direcao das vigas foi escolhida. Reinterpreta DirecaoVigasGraus/ArestaReferencia.</summary>
        public OrientacaoVigasModo ModoOrientacao { get; set; } = OrientacaoVigasModo.Automatica;

        /// <summary>Indice da aresta do contorno usada quando o modo e Paralela/Perpendicular a borda.</summary>
        public int ArestaReferencia { get; set; }

        public ContornoLaje Contorno { get; set; }

        /// <summary>Cota Z (mm, absoluta no projeto) da face superior do conjunto acabado.</summary>
        public double CotaMm { get; set; }

        /// <summary>Espessura da chapa da telha, em mm; so vale para laje umida. Padrao inicial 0,43.</summary>
        public double EspessuraTelhaMm { get; set; } = TelhaTp40.Espessura043Mm;

        /// <summary>
        /// Composicao de placas da laje seca. Nenhuma = comportamento padrao (placa unica do
        /// catalogo). As demais usam as espessuras abaixo para montar as camadas e gerar as placas.
        /// </summary>
        public ComposicaoTipo ComposicaoPlacas { get; set; } = ComposicaoTipo.Nenhuma;

        /// <summary>Espessura da placa OSB, em mm (ver ComposicaoCobertura.EspessurasOSBMm).</summary>
        public double EspessuraOSBMm { get; set; } = 15.0;

        /// <summary>Espessura da placa cimenticia, em mm (ver ComposicaoCobertura.EspessurasCimenticiaMm).</summary>
        public double EspessuraCimenticiaMm { get; set; } = 10.0;

        /// <summary>Nivel de detalhe dos fixadores (parafusos). Oculto = nao gera nenhum.</summary>
        public NivelDetalheFixadores NivelFixadores { get; set; } = NivelDetalheFixadores.Oculto;

        /// <summary>Passo entre fixadores de placa ao longo da viga (mm) -- valor de partida.</summary>
        public double EspacamentoFixadoresPlacaMm { get; set; } = FixadoresLaje.EspacamentoPlacaPadraoMm;

        /// <summary>
        /// Modela o enchimento das ondas da telha com argamassa (laje umida). Se falso, so a placa
        /// plana acima da onda e gerada.
        /// </summary>
        public bool PreencherOndas { get; set; } = true;

        /// <summary>
        /// Gera as fitas tensionadas que travam as trelicas entre si (so vigas trelicadas). A
        /// disposicao e geometrica de partida, NAO normativa (ver FitasTensionadasLaje).
        /// </summary>
        public bool FitasTensionadas { get; set; } = false;

        /// <summary>Passo alvo (mm) entre fitas ao longo do vao -- geometrico de partida, nao normativo.</summary>
        public double EspacamentoFitasTensionadasMm { get; set; } = FitasTensionadasLaje.PassoPadraoMm;

        public ComposicaoCobertura Composicao()
        {
            return ComposicaoCobertura.Criar(ComposicaoPlacas, EspessuraOSBMm, EspessuraCimenticiaMm);
        }

        /// <summary>
        /// Altura efetiva da viga (mm) usada no empilhamento de camadas: para trelica e a altura
        /// entre eixos dos banzos (AlturaTrelicaMm); para os demais tipos e a largura da alma do perfil.
        /// </summary>
        public double AlturaVigaEfetivaMm()
        {
            if (TipoViga == TipoVigaLaje.Trelica) return AlturaTrelicaMm;
            return Perfil != null ? Perfil.LarguraAlmaMm : 0.0;
        }

        public List<string> Validar()
        {
            var erros = new List<string>();

            if (Contorno == null) erros.Add("Contorno nao informado.");
            else erros.AddRange(Contorno.Validar());

            if (Perfil == null)
            {
                erros.Add("Perfil nao informado.");
            }
            else if (!CatalogoPerfisLaje.EhPerfilDeLaje(Perfil))
            {
                erros.Add(
                    "Perfil invalido para laje: use Steel Frame 90/140/200 mm com chapa 0,95 ou 1,25 mm"
                    + " (sem Drywall nem chapa 0,80 mm).");
            }

            if (double.IsNaN(EspacamentoMm) || double.IsInfinity(EspacamentoMm) || EspacamentoMm <= 0.0)
                erros.Add("Espacamento deve ser maior que zero.");

            if (DirecaoVigasGraus.HasValue
                && (double.IsNaN(DirecaoVigasGraus.Value) || double.IsInfinity(DirecaoVigasGraus.Value)))
                erros.Add("Direcao das vigas deve ser um numero finito.");

            if (double.IsNaN(CotaMm) || double.IsInfinity(CotaMm))
                erros.Add("Cota deve ser um numero finito.");

            bool vigaImplementada = false;
            foreach (var tv in CatalogoTiposVigaLaje.Todos)
            {
                if (tv.Tipo == TipoViga && tv.Implementado)
                {
                    vigaImplementada = true;
                    break;
                }
            }
            if (!vigaImplementada)
            {
                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Tipo de viga nao implementado: {0}.", TipoViga));
            }

            if (TipoViga == TipoVigaLaje.Trelica)
            {
                if (!CatalogoTrelica.Implementado(Trelica))
                {
                    erros.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Tipo de trelica nao implementado: {0}.", Trelica));
                }

                if (double.IsNaN(AlturaTrelicaMm) || double.IsInfinity(AlturaTrelicaMm) || AlturaTrelicaMm <= 0.0)
                {
                    erros.Add("Altura da trelica deve ser maior que zero.");
                }

                if (NumeroPaineisTrelica.HasValue
                    && NumeroPaineisTrelica.Value < ComposicaoTrelica.NumeroPaineisMinimo)
                {
                    erros.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Numero de paineis da trelica deve ser no minimo {0}.",
                        ComposicaoTrelica.NumeroPaineisMinimo));
                }

                if (double.IsNaN(PainelTrelicaAlvoMm) || double.IsInfinity(PainelTrelicaAlvoMm)
                    || PainelTrelicaAlvoMm <= 0.0)
                {
                    erros.Add("Comprimento alvo de painel da trelica deve ser maior que zero.");
                }
            }

            if (FitasTensionadas && TipoViga == TipoVigaLaje.Trelica
                && (double.IsNaN(EspacamentoFitasTensionadasMm)
                    || double.IsInfinity(EspacamentoFitasTensionadasMm)
                    || EspacamentoFitasTensionadasMm <= 0.0))
            {
                erros.Add("Passo das fitas tensionadas deve ser maior que zero.");
            }

            if (NivelFixadores != NivelDetalheFixadores.Oculto
                && (double.IsNaN(EspacamentoFixadoresPlacaMm) || double.IsInfinity(EspacamentoFixadoresPlacaMm)
                    || EspacamentoFixadoresPlacaMm <= 0.0))
            {
                erros.Add("Espacamento dos fixadores de placa deve ser maior que zero.");
            }

            DescricaoSistemaLaje descricaoSistema = CatalogoSistemasLaje.Obter(Sistema);
            if (descricaoSistema == null || !descricaoSistema.Implementado)
                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Sistema de laje nao implementado: {0}.", Sistema));

            if (ModoOrientacao == OrientacaoVigasModo.Manual && !DirecaoVigasGraus.HasValue)
                erros.Add("Direcao manual escolhida, mas nenhum angulo (em graus) foi informado.");

            if ((ModoOrientacao == OrientacaoVigasModo.ParalelaAresta
                    || ModoOrientacao == OrientacaoVigasModo.PerpendicularAresta)
                && Contorno != null)
            {
                List<string> errosAresta;
                ResolvedorOrientacaoVigas.AnguloDaAresta(Contorno, ArestaReferencia, out errosAresta);
                erros.AddRange(errosAresta);
            }

            if (Tipo == TipoLaje.Seca && ComposicaoPlacas != ComposicaoTipo.Nenhuma)
            {
                if (ComposicaoCobertura.UsaOSB(ComposicaoPlacas)
                    && !ComposicaoCobertura.EspessuraOSBValida(EspessuraOSBMm))
                {
                    erros.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Espessura de OSB invalida ({0:F0} mm); use 15, 16, 22 ou 25 mm.", EspessuraOSBMm));
                }

                if (ComposicaoCobertura.UsaCimenticia(ComposicaoPlacas)
                    && !ComposicaoCobertura.EspessuraCimenticiaValida(EspessuraCimenticiaMm))
                {
                    erros.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Espessura de placa cimenticia invalida ({0:F0} mm); use 10 ou 20 mm.",
                        EspessuraCimenticiaMm));
                }
            }

            if (Tipo == TipoLaje.Umida && !TelhaTp40.EspessuraDisponivel(EspessuraTelhaMm))
            {
                string disponiveis = string.Join(
                    " ou ",
                    Array.ConvertAll(
                        TelhaTp40.EspessurasDisponiveisMm,
                        e => e.ToString("F2", CultureInfo.InvariantCulture)));

                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Espessura de telha invalida ({0:F2} mm); use {1} mm.", EspessuraTelhaMm, disponiveis));
            }

            return erros;
        }

        /// <summary>
        /// Direcao (graus) a passar para a OrientacaoVigas, resolvendo o modo escolhido
        /// (automatica -> null; manual -> DirecaoVigasGraus; paralela/perpendicular -> aresta).
        /// <paramref name="erros"/> recebe as falhas (lista vazia = sucesso).
        /// </summary>
        public double? DirecaoEfetivaGraus(out List<string> erros)
        {
            return ResolvedorOrientacaoVigas.DirecaoGraus(
                Contorno, ModoOrientacao, ArestaReferencia, DirecaoVigasGraus, out erros);
        }

        public bool EhPorBordaDeReferencia()
        {
            return OrientacaoHelper.EhPorBorda(ModoOrientacao);
        }
    }
}
