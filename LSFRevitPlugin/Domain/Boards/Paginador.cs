using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LSFRevitPlugin.Domain.Boards
{
    /// <summary>
    /// Retangulo no plano da parede, em mm: U ao longo da parede (a partir do inicio do painel),
    /// Z na vertical (a partir do piso).
    /// </summary>
    public sealed class RetanguloMm
    {
        public RetanguloMm(double u0, double u1, double z0, double z1)
        {
            U0 = u0;
            U1 = u1;
            Z0 = z0;
            Z1 = z1;
        }

        public double U0 { get; }
        public double U1 { get; }
        public double Z0 { get; }
        public double Z1 { get; }

        public double Largura { get { return U1 - U0; } }
        public double Altura { get { return Z1 - Z0; } }
        public double Area { get { return Largura * Altura; } }
        public bool TemArea { get { return Largura > 0.5 && Altura > 0.5; } }

        /// <summary>Intersecao de dois retangulos (pode ter largura/altura negativas se nao se tocam).</summary>
        public static RetanguloMm Interseccao(RetanguloMm a, RetanguloMm b)
        {
            return new RetanguloMm(
                Math.Max(a.U0, b.U0),
                Math.Min(a.U1, b.U1),
                Math.Max(a.Z0, b.Z0),
                Math.Min(a.Z1, b.Z1));
        }
    }

    /// <summary>Dados de uma face de um painel a paginar. Tudo em mm, em coordenadas do painel.</summary>
    public sealed class EntradaPaginacao
    {
        public int NumeroPainel { get; set; } = 1;
        public int NumeroFace { get; set; } = 1;

        /// <summary>
        /// Inicio do codigo das pecas (por exemplo "1234-P2" gera "1234-P2-F1-03"). Se vazio,
        /// usa "P" + NumeroPainel.
        /// </summary>
        public string PrefixoCodigo { get; set; }

        public double LarguraPainelMm { get; set; }
        public double AlturaMm { get; set; }

        /// <summary>Aberturas (portas/janelas) ja recortadas para dentro do painel.</summary>
        public List<RetanguloMm> Aberturas { get; set; } = new List<RetanguloMm>();

        /// <summary>Posicao (eixo) de cada montante onde uma junta pode cair, ao longo do painel.</summary>
        public List<double> PosicoesMontantesMm { get; set; } = new List<double>();

        public TipoPlaca Tipo { get; set; }
        public MedidaPlaca Medida { get; set; }
        public OrientacaoPlaca Orientacao { get; set; } = OrientacaoPlaca.EmPe;

        /// <summary>Regra do tipo de placa; se nula, usa a do catalogo.</summary>
        public RegraFixacaoPlaca Regra { get; set; }

        /// <summary>
        /// 0 ou 1. Troca o padrao de desencontro: use valores diferentes nas duas faces da mesma
        /// parede para que as juntas de uma face nao caiam nos mesmos montantes da outra.
        /// </summary>
        public int IndiceDeDesencontro { get; set; }

        /// <summary>
        /// Menor altura de fiada (e menor largura absoluta de uma peca) aceita, em mm. Pecas menores
        /// que isso nao sao usadas: a junta recua para outro montante. Pecas que ficam menores
        /// por causa de aberturas ou cortes saem como aviso.
        /// </summary>
        public double LarguraMinimaPecaMm { get; set; } = 200.0;

        /// <summary>
        /// Menor largura, em mm, de uma peca vertical (faixa em pe entre duas juntas verticais,
        /// medida ao longo da parede). Vale para as pecas das pontas e do meio da fiada. Uma peca
        /// menor que isso so e aceita quando uma abertura (porta, janela ou encontro de parede) esta
        /// mais perto que isso e a corta, ou quando o painel inteiro e mais estreito que isso.
        /// </summary>
        public double LarguraMinimaVerticalMm { get; set; } = 400.0;

        /// <summary>
        /// Distancia minima, em mm, entre uma junta e a quina de uma abertura. Vale para a linha
        /// vertical das quinas em todas as fiadas (a junta nao fica alinhada com a quina) e para a
        /// altura das arestas nas juntas horizontais. 0 desliga a regra.
        /// </summary>
        public double DistanciaMinimaVerticeMm { get; set; } = 200.0;

        /// <summary>
        /// true quando a ponta final do painel e um canto em L ou um T. As placas sao assentadas a
        /// partir do inicio e tambem a partir do fim: as duas pontas ficam com a maior placa
        /// possivel e a peca cortada cai no meio, sobre um montante.
        /// </summary>
        public bool AncorarNoFim { get; set; }
    }

    /// <summary>Uma peca de placa ja posicionada (placa inteira ou recorte).</summary>
    public sealed class PecaPlaca
    {
        /// <summary>Etiqueta da peca, por exemplo "P2-F1-03".</summary>
        public string Codigo { get; set; }

        /// <summary>Numero da fiada (de baixo para cima), a partir de 1.</summary>
        public int Fiada { get; set; }

        /// <summary>Retangulo que contem a peca (sem os recortes).</summary>
        public RetanguloMm Contorno { get; set; }

        /// <summary>Partes do contorno que realmente tem placa (o contorno menos os recortes).</summary>
        public List<RetanguloMm> Celulas { get; } = new List<RetanguloMm>();

        /// <summary>Recortes (aberturas) dentro do contorno; cada um e etiquetado como Codigo-R1, -R2...</summary>
        public List<RetanguloMm> Recortes { get; } = new List<RetanguloMm>();

        /// <summary>Codigo da placa fisica de onde a peca foi cortada, por exemplo "GST-003".</summary>
        public string PlacaOrigem { get; set; }

        /// <summary>true se a peca saiu de uma sobra de outro corte.</summary>
        public bool Reaproveitada { get; set; }

        /// <summary>Quando reaproveitada, a placa de onde veio a sobra.</summary>
        public string OrigemDaSobra { get; set; }

        /// <summary>true se a peca e uma placa inteira (sem corte nem recorte).</summary>
        public bool Inteira { get; set; }

        public bool Estreita { get; set; }
        public bool ExcedePlaca { get; set; }

        public double AreaMm2 { get { return Celulas.Sum(c => c.Area); } }

        public string CodigoDoRecorte(int indiceZeroBase)
        {
            return Codigo + "-R" + (indiceZeroBase + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Junta entre duas pecas vizinhas (para o relatorio de rejunte).</summary>
    public sealed class JuntaPlaca
    {
        public string CodigoA { get; set; }
        public string CodigoB { get; set; }
        public TipoPlaca TipoA { get; set; }
        public TipoPlaca TipoB { get; set; }

        /// <summary>true = junta vertical (linha de U constante); false = junta horizontal.</summary>
        public bool Vertical { get; set; }

        public double ComprimentoMm { get; set; }

        /// <summary>true quando as duas placas sao de tipos diferentes.</summary>
        public bool Mista { get { return TipoA != TipoB; } }
    }

    /// <summary>Resultado da paginacao de uma face de um painel.</summary>
    public sealed class ResultadoPaginacao
    {
        public int NumeroPainel { get; set; }
        public int NumeroFace { get; set; }
        public TipoPlaca Tipo { get; set; }
        public OrientacaoPlaca Orientacao { get; set; }
        public MedidaPlaca Medida { get; set; }
        public double LarguraPainelMm { get; set; }
        public double AlturaMm { get; set; }

        public List<PecaPlaca> Pecas { get; } = new List<PecaPlaca>();
        public List<JuntaPlaca> Juntas { get; } = new List<JuntaPlaca>();

        /// <summary>Posicao (eixo de montante) de cada junta vertical, uma lista por fiada.</summary>
        public List<List<double>> JuntasVerticaisPorFiada { get; } = new List<List<double>>();

        public List<string> Avisos { get; } = new List<string>();

        public double AreaPecasMm2 { get { return Pecas.Sum(p => p.AreaMm2); } }
    }

    /// <summary>Sobra de um corte, disponivel para outra peca.</summary>
    public sealed class Sobra
    {
        public string PlacaOrigem { get; set; }
        public double LadoA { get; set; }
        public double LadoB { get; set; }
        public double Area { get { return LadoA * LadoB; } }
    }

    /// <summary>De onde saiu uma peca: de uma sobra ou de uma placa nova.</summary>
    public sealed class RetiradaDePeca
    {
        public string PlacaOrigem { get; set; }
        public bool Reaproveitada { get; set; }
        public string OrigemDaSobra { get; set; }
        public bool ExcedeuPlaca { get; set; }
    }

    /// <summary>
    /// Estoque de placas de UM tipo e UMA medida: controla quantas placas novas foram usadas e
    /// quais sobras ainda podem ser reaproveitadas. Use o mesmo estoque em todos os paineis e
    /// faces do mesmo tipo/medida para que as sobras sejam compartilhadas.
    /// </summary>
    public sealed class EstoquePlacas
    {
        private readonly List<Sobra> _sobras = new List<Sobra>();
        private int _contador;

        public EstoquePlacas(TipoPlaca tipo, MedidaPlaca medida)
        {
            if (medida == null) throw new ArgumentNullException(nameof(medida));
            Tipo = tipo;
            Medida = medida;
        }

        public TipoPlaca Tipo { get; }
        public MedidaPlaca Medida { get; }

        /// <summary>Sobra com algum lado menor que isso e considerada perda.</summary>
        public double MinimoSobraMm { get; set; } = 100.0;

        public int PlacasUsadas { get { return _contador; } }

        public double AreaTotalMm2
        {
            get { return _contador * Medida.LadoCurtoMm * Medida.LadoLongoMm; }
        }

        public IReadOnlyList<Sobra> Sobras { get { return _sobras; } }

        public double AreaDeSobrasMm2 { get { return _sobras.Sum(s => s.Area); } }

        /// <summary>Guarda uma sobra (se for grande o bastante).</summary>
        public void AdicionarSobra(string placaOrigem, double ladoA, double ladoB)
        {
            if (Math.Min(ladoA, ladoB) < MinimoSobraMm) return;

            _sobras.Add(new Sobra { PlacaOrigem = placaOrigem, LadoA = ladoA, LadoB = ladoB });
        }

        /// <summary>
        /// Tira uma peca de a x b mm: primeiro tenta a menor sobra em que ela cabe (podendo girar a
        /// peca 90 graus); se nenhuma servir, abre uma placa nova. As sobras do corte voltam ao estoque.
        /// </summary>
        public RetiradaDePeca Retirar(double a, double b, bool permitirGirar)
        {
            const double eps = 0.5;

            Sobra melhor = null;
            bool melhorGirada = false;

            foreach (Sobra s in _sobras)
            {
                bool direto = a <= s.LadoA + eps && b <= s.LadoB + eps;
                bool girado = permitirGirar && b <= s.LadoA + eps && a <= s.LadoB + eps;
                if (!direto && !girado) continue;

                if (melhor == null || s.Area < melhor.Area)
                {
                    melhor = s;
                    melhorGirada = !direto;
                }
            }

            if (melhor != null)
            {
                _sobras.Remove(melhor);

                double pa = melhorGirada ? b : a;
                double pb = melhorGirada ? a : b;
                DividirSobra(melhor.PlacaOrigem, melhor.LadoA, melhor.LadoB, pa, pb);

                return new RetiradaDePeca
                {
                    PlacaOrigem = melhor.PlacaOrigem,
                    Reaproveitada = true,
                    OrigemDaSobra = melhor.PlacaOrigem
                };
            }

            double curto = Medida.LadoCurtoMm;
            double longo = Medida.LadoLongoMm;
            bool cabeDireto = a <= curto + eps && b <= longo + eps;
            bool cabeGirado = permitirGirar && b <= curto + eps && a <= longo + eps;

            _contador++;
            string id = Paginador.CodigoCurto(Tipo) + "-" + _contador.ToString("000", CultureInfo.InvariantCulture);

            if (!cabeDireto && !cabeGirado)
            {
                return new RetiradaDePeca { PlacaOrigem = id, ExcedeuPlaca = true };
            }

            double qa = cabeDireto ? a : b;
            double qb = cabeDireto ? b : a;
            DividirSobra(id, curto, longo, qa, qb);

            return new RetiradaDePeca { PlacaOrigem = id };
        }

        /// <summary>
        /// Corte em guilhotina: tira a peca (pa x pb) do canto de uma placa (sa x sb) e devolve ao
        /// estoque as duas faixas que sobram, escolhendo a divisao que deixa a maior faixa.
        /// </summary>
        private void DividirSobra(string origem, double sa, double sb, double pa, double pb)
        {
            double lateral = sa - pa;
            double cima = sb - pb;

            double area1 = Math.Max(lateral * sb, pa * cima);
            double area2 = Math.Max(lateral * pb, sa * cima);

            if (area1 >= area2)
            {
                AdicionarSobra(origem, lateral, sb);
                AdicionarSobra(origem, pa, cima);
            }
            else
            {
                AdicionarSobra(origem, lateral, pb);
                AdicionarSobra(origem, sa, cima);
            }
        }
    }

    /// <summary>Um estoque por tipo e medida de placa, para o projeto inteiro.</summary>
    public sealed class ColecaoDeEstoques
    {
        private readonly Dictionary<string, EstoquePlacas> _estoques = new Dictionary<string, EstoquePlacas>();

        public EstoquePlacas Obter(TipoPlaca tipo, MedidaPlaca medida)
        {
            string chave = tipo.ToString() + "|"
                + Math.Round(medida.LadoCurtoMm).ToString(CultureInfo.InvariantCulture) + "x"
                + Math.Round(medida.LadoLongoMm).ToString(CultureInfo.InvariantCulture);

            EstoquePlacas estoque;
            if (!_estoques.TryGetValue(chave, out estoque))
            {
                estoque = new EstoquePlacas(tipo, medida);
                _estoques[chave] = estoque;
            }

            return estoque;
        }

        public IEnumerable<EstoquePlacas> Todos { get { return _estoques.Values; } }
    }

    /// <summary>
    /// Paginacao das placas de uma face de um painel: onde cada placa fica, com as juntas
    /// verticais sobre montantes e desencontradas, afastadas das quinas das aberturas, os
    /// recortes dos vaos e o reaproveitamento das sobras. Trabalha so com medidas (mm), sem
    /// depender do Revit.
    ///
    /// Em duas etapas: PaginarSemEstoque desenha as pecas de uma face; Alocar distribui as pecas
    /// de varias faces (o projeto inteiro) sobre as placas e sobras, da maior para a menor, para
    /// aproveitar o maximo. Paginar faz as duas etapas de uma face so.
    /// </summary>
    public static class Paginador
    {
        private const double Eps = 0.5;
        private const double ToleranciaCelula = 0.01;
        private const double ToleranciaJunta = 0.75;

        /// <summary>Distancia minima desejada entre juntas verticais de fiadas vizinhas.</summary>
        public const double LimiteDesencontroMm = 400.0;

        /// <summary>Sigla do tipo de placa, usada no codigo da placa fisica (por exemplo GST-001).</summary>
        public static string CodigoCurto(TipoPlaca tipo)
        {
            switch (tipo)
            {
                case TipoPlaca.Cimenticia: return "CIM";
                case TipoPlaca.GlasrocX: return "GLX";
                case TipoPlaca.Osb: return "OSB";
                case TipoPlaca.GessoRu: return "GRU";
                case TipoPlaca.GessoSt: return "GST";
                case TipoPlaca.GessoRf: return "GRF";
                case TipoPlaca.GessoPerforma: return "GPF";
                case TipoPlaca.PerformaRu: return "PRU";
                default: return "PLC";
            }
        }

        /// <summary>Menor distancia entre dois conjuntos de posicoes (infinito se algum estiver vazio).</summary>
        public static double MenorDistancia(IList<double> a, IList<double> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return double.PositiveInfinity;

            double menor = double.PositiveInfinity;
            foreach (double x in a)
            {
                foreach (double y in b)
                {
                    double d = Math.Abs(x - y);
                    if (d < menor) menor = d;
                }
            }

            return menor;
        }

        /// <summary>Desenha e aloca as pecas de uma face usando o estoque dado.</summary>
        public static ResultadoPaginacao Paginar(EntradaPaginacao entrada, EstoquePlacas estoque)
        {
            if (estoque == null) throw new ArgumentNullException(nameof(estoque));

            ResultadoPaginacao resultado = PaginarSemEstoque(entrada);
            Alocar(new[] { resultado }, estoque);
            return resultado;
        }

        /// <summary>
        /// Distribui as pecas de varias faces (todas do mesmo tipo e medida) sobre as placas e
        /// sobras do estoque. As pecas de todas as faces entram juntas, da maior para a menor, e
        /// cada uma usa a menor sobra em que cabe; so abre placa nova quando nenhuma sobra serve.
        /// </summary>
        public static void Alocar(IEnumerable<ResultadoPaginacao> resultados, EstoquePlacas estoque)
        {
            if (resultados == null) throw new ArgumentNullException(nameof(resultados));
            if (estoque == null) throw new ArgumentNullException(nameof(estoque));

            var itens = new List<KeyValuePair<ResultadoPaginacao, PecaPlaca>>();
            foreach (ResultadoPaginacao resultado in resultados)
            {
                foreach (PecaPlaca peca in resultado.Pecas)
                {
                    itens.Add(new KeyValuePair<ResultadoPaginacao, PecaPlaca>(resultado, peca));
                }
            }

            foreach (KeyValuePair<ResultadoPaginacao, PecaPlaca> item in itens.OrderByDescending(i => i.Value.Contorno.Area))
            {
                ResultadoPaginacao dono = item.Key;
                PecaPlaca peca = item.Value;

                RetiradaDePeca retirada = estoque.Retirar(peca.Contorno.Largura, peca.Contorno.Altura, true);

                peca.PlacaOrigem = retirada.PlacaOrigem;
                peca.Reaproveitada = retirada.Reaproveitada;
                peca.OrigemDaSobra = retirada.OrigemDaSobra;
                peca.ExcedePlaca = retirada.ExcedeuPlaca;

                if (retirada.ExcedeuPlaca)
                {
                    dono.Avisos.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "A peca {0} ({1:0} x {2:0} mm) e maior que a placa; conferir a medida ou a orientacao.",
                        peca.Codigo,
                        peca.Contorno.Largura,
                        peca.Contorno.Altura));
                }

                // O recorte de um vao que encosta na borda da peca sai da placa como um retangulo
                // inteiro e volta ao estoque.
                foreach (RetanguloMm recorte in peca.Recortes)
                {
                    if (TocaBorda(recorte, peca.Contorno))
                    {
                        estoque.AdicionarSobra(retirada.PlacaOrigem, recorte.Largura, recorte.Altura);
                    }
                }
            }
        }

        /// <summary>
        /// Desenha as pecas de uma face: fiadas, juntas, recortes e codigos. Nao mexe em estoque:
        /// PlacaOrigem e Reaproveitada ficam vazios ate Alocar.
        /// </summary>
        public static ResultadoPaginacao PaginarSemEstoque(EntradaPaginacao entrada)
        {
            if (entrada == null) throw new ArgumentNullException(nameof(entrada));
            if (entrada.Medida == null)
                throw new ArgumentException("Informe a medida da placa.", nameof(entrada));
            if (entrada.LarguraPainelMm <= 0 || entrada.AlturaMm <= 0)
                throw new ArgumentException("A largura e a altura do painel devem ser maiores que zero.", nameof(entrada));

            RegraFixacaoPlaca regra = entrada.Regra ?? PlacaCatalogo.RegraDoTipo(entrada.Tipo);
            double gap = regra.JuntaEntrePlacasMm;
            double piso = regra.FolgaDoPisoMm;
            double minPeca = entrada.LarguraMinimaPecaMm;
            double minVertical = entrada.LarguraMinimaVerticalMm;
            double distanciaVertice = entrada.DistanciaMinimaVerticeMm;

            double curto = entrada.Medida.LadoCurtoMm;
            double longo = entrada.Medida.LadoLongoMm;
            bool emPe = entrada.Orientacao == OrientacaoPlaca.EmPe;

            // Em pe: a placa tem o lado curto ao longo da parede e o longo na vertical.
            // Deitada: o contrario.
            double spanU = emPe ? curto : longo;
            double alturaFiada = emPe ? longo : curto;

            var resultado = new ResultadoPaginacao
            {
                NumeroPainel = entrada.NumeroPainel,
                NumeroFace = entrada.NumeroFace,
                Tipo = entrada.Tipo,
                Orientacao = entrada.Orientacao,
                Medida = entrada.Medida,
                LarguraPainelMm = entrada.LarguraPainelMm,
                AlturaMm = entrada.AlturaMm
            };

            List<double[]> fiadas = CalcularFiadas(
                entrada.AlturaMm, piso, gap, alturaFiada, minPeca, entrada.Aberturas, distanciaVertice);
            if (fiadas.Count == 0)
            {
                resultado.Avisos.Add("A altura do painel e menor que a folga do piso: nenhuma placa foi gerada.");
                return resultado;
            }

            List<double> montantes = entrada.PosicoesMontantesMm.OrderBy(x => x).ToList();

            // Posicao U da linha vertical de cada quina de abertura (as que ficam na borda do painel nao entram).
            var verticesU = new List<double>();
            foreach (RetanguloMm abertura in entrada.Aberturas)
            {
                foreach (double u in new[] { abertura.U0, abertura.U1 })
                {
                    if (u <= Eps || u >= entrada.LarguraPainelMm - Eps) continue;
                    verticesU.Add(u);
                }
            }

            // Montantes onde a junta pode cair: a pelo menos distanciaVertice da linha das quinas,
            // em qualquer fiada (assim a junta nunca fica alinhada com a quina).
            List<double> elegiveis = MontantesAfastadosDosVertices(montantes, verticesU, distanciaVertice);

            string prefixoCodigo = string.IsNullOrEmpty(entrada.PrefixoCodigo)
                ? "P" + entrada.NumeroPainel.ToString(CultureInfo.InvariantCulture)
                : entrada.PrefixoCodigo;

            var novasPecas = new List<PecaPlaca>();
            List<double> juntasDaFiadaAnterior = null;

            for (int r = 0; r < fiadas.Count; r++)
            {
                // Fiadas alternadas (e faces alternadas) comecam com meia placa: juntas desencontradas.
                bool desloca = ((r + entrada.IndiceDeDesencontro) % 2) == 1;

                List<double[]> zonas = ZonasPreferidas(
                    entrada.Aberturas, fiadas[r][0], fiadas[r][1], distanciaVertice);

                double deslocamentoInicial = desloca
                    ? DeslocamentoInicial(elegiveis, spanU, gap, entrada.LarguraPainelMm, juntasDaFiadaAnterior, minVertical)
                    : 0.0;

                bool foraDeMontante;
                bool pertoDeVertice;
                List<double> juntas = CalcularJuntasDaFiada(
                    entrada.LarguraPainelMm, spanU, gap, elegiveis, montantes, zonas,
                    deslocamentoInicial, minVertical, juntasDaFiadaAnterior,
                    out foraDeMontante, out pertoDeVertice);

                // Canto ou T na ponta final: assenta tambem a partir do fim, para o canto ficar
                // com placa grande e a peca cortada cair no meio.
                if (entrada.AncorarNoFim)
                {
                    List<double> ancoradas = JuntasAncoradasNasDuasPontas(
                        juntas, entrada.LarguraPainelMm, spanU, gap, elegiveis, montantes, zonas,
                        desloca, minVertical, minPeca, juntasDaFiadaAnterior);
                    if (ancoradas != null) juntas = ancoradas;
                }

                resultado.JuntasVerticaisPorFiada.Add(juntas);
                juntasDaFiadaAnterior = juntas;

                if (foraDeMontante)
                {
                    resultado.Avisos.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Fiada {0}: nao ha montante onde a junta deveria cair; junta fora de montante.",
                        r + 1));
                }

                if (pertoDeVertice)
                {
                    resultado.Avisos.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Fiada {0}: nao ha montante afastado das aberturas onde a junta deveria cair; junta a menos de {1:0} mm da linha da quina de uma abertura.",
                        r + 1,
                        distanciaVertice));
                }

                for (int k = 0; k <= juntas.Count; k++)
                {
                    double u0 = k == 0 ? 0.0 : juntas[k - 1] + gap / 2.0;
                    double u1 = k == juntas.Count ? entrada.LarguraPainelMm : juntas[k] - gap / 2.0;
                    if (u1 - u0 < Eps) continue;

                    var contorno = new RetanguloMm(u0, u1, fiadas[r][0], fiadas[r][1]);

                    foreach (Componente comp in SepararPorAberturas(contorno, entrada.Aberturas))
                    {
                        var peca = new PecaPlaca { Fiada = r + 1, Contorno = comp.Contorno };
                        peca.Celulas.AddRange(comp.Celulas);

                        foreach (RetanguloMm abertura in entrada.Aberturas)
                        {
                            RetanguloMm inter = RetanguloMm.Interseccao(abertura, comp.Contorno);
                            if (inter.TemArea) peca.Recortes.Add(inter);
                        }

                        // Menor que o minimo absoluto: sempre avisa (mesmo quando e uma abertura que
                        // deixa a peca assim). Entre o minimo absoluto e o minimo vertical: avisa so se
                        // nao foi uma abertura que cortou a peca e se o painel e largo o bastante.
                        bool abaixoDoMinimoAbsoluto =
                            comp.Contorno.Largura < minPeca - Eps || comp.Contorno.Altura < minPeca - Eps;
                        bool cortadaPorAbertura = comp.Contorno.Largura < (u1 - u0) - Eps;
                        bool painelLargo = entrada.LarguraPainelMm >= minVertical + Eps;
                        bool abaixoDoMinimoVertical = !cortadaPorAbertura && painelLargo
                            && comp.Contorno.Largura + gap < minVertical - Eps;

                        peca.Estreita = abaixoDoMinimoAbsoluto || abaixoDoMinimoVertical;
                        peca.Inteira = peca.Recortes.Count == 0
                            && Math.Abs(comp.Contorno.Largura - spanU) <= gap + Eps
                            && Math.Abs(comp.Contorno.Altura - alturaFiada) <= gap + Eps;

                        novasPecas.Add(peca);
                    }
                }
            }

            if (montantes.Count == 0 && resultado.JuntasVerticaisPorFiada.Any(j => j.Count > 0))
            {
                resultado.Avisos.Add("Nenhum montante informado: as juntas nao foram alinhadas a montantes.");
            }

            // Etiquetas na ordem de leitura: fiada de baixo para cima, da esquerda para a direita.
            for (int i = 0; i < novasPecas.Count; i++)
            {
                novasPecas[i].Codigo = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}-F{1}-{2:00}",
                    prefixoCodigo,
                    entrada.NumeroFace,
                    i + 1);
            }

            foreach (PecaPlaca peca in novasPecas.Where(p => p.Estreita))
            {
                bool soMinimoVertical = peca.Contorno.Largura >= minPeca - Eps && peca.Contorno.Altura >= minPeca - Eps;
                double minimoCitado = soMinimoVertical ? minVertical : minPeca;

                resultado.Avisos.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "A peca {0} e menor que o minimo ({1:0} x {2:0} mm; minimo {3:0} mm).",
                    peca.Codigo,
                    peca.Contorno.Largura,
                    peca.Contorno.Altura,
                    minimoCitado));
            }

            resultado.Pecas.AddRange(novasPecas);

            CalcularJuntasInternas(resultado, gap);

            for (int r = 0; r + 1 < resultado.JuntasVerticaisPorFiada.Count; r++)
            {
                double d = MenorDistancia(resultado.JuntasVerticaisPorFiada[r], resultado.JuntasVerticaisPorFiada[r + 1]);
                if (!double.IsInfinity(d) && d < LimiteDesencontroMm - Eps)
                {
                    resultado.Avisos.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Fiadas {0} e {1}: juntas verticais a {2:0} mm uma da outra (desejavel: {3:0} mm ou mais).",
                        r + 1,
                        r + 2,
                        d,
                        LimiteDesencontroMm));
                }
            }

            // Junta horizontal que nao deu para afastar das arestas das aberturas.
            if (distanciaVertice > 0)
            {
                for (int r = 0; r + 1 < fiadas.Count; r++)
                {
                    double zJunta = (fiadas[r][1] + fiadas[r + 1][0]) / 2.0;

                    foreach (RetanguloMm abertura in entrada.Aberturas)
                    {
                        double d = Math.Min(Math.Abs(zJunta - abertura.Z0), Math.Abs(zJunta - abertura.Z1));
                        if (d < distanciaVertice - Eps)
                        {
                            resultado.Avisos.Add(string.Format(
                                CultureInfo.InvariantCulture,
                                "Fiadas {0} e {1}: a junta horizontal fica a {2:0} mm de uma aresta de abertura (desejavel: {3:0} mm ou mais).",
                                r + 1,
                                r + 2,
                                d,
                                distanciaVertice));
                            break;
                        }
                    }
                }
            }

            if (resultado.Juntas.Any(j => !j.Vertical))
            {
                resultado.Avisos.Add("Ha juntas horizontais entre fiadas, sem montante atras: prever travessa (bloqueador) nessas alturas.");
            }

            return resultado;
        }

        /// <summary>
        /// Juntas entre as placas de dois paineis vizinhos na mesma face: as pecas que encostam no
        /// fim do painel da esquerda e no inicio do painel da direita. E aqui que aparece a junta
        /// "mista" quando os dois paineis usam tipos de placa diferentes.
        /// </summary>
        public static List<JuntaPlaca> JuntasDeDivisa(ResultadoPaginacao esquerdo, ResultadoPaginacao direito)
        {
            if (esquerdo == null) throw new ArgumentNullException(nameof(esquerdo));
            if (direito == null) throw new ArgumentNullException(nameof(direito));

            var lista = new List<JuntaPlaca>();

            foreach (PecaPlaca a in esquerdo.Pecas)
            {
                foreach (RetanguloMm ca in a.Celulas)
                {
                    if (Math.Abs(ca.U1 - esquerdo.LarguraPainelMm) > Eps) continue;

                    foreach (PecaPlaca b in direito.Pecas)
                    {
                        foreach (RetanguloMm cb in b.Celulas)
                        {
                            if (Math.Abs(cb.U0) > Eps) continue;

                            double sobreposicao = Math.Min(ca.Z1, cb.Z1) - Math.Max(ca.Z0, cb.Z0);
                            if (sobreposicao > Eps)
                            {
                                Acumular(lista, a, b, true, sobreposicao, esquerdo.Tipo, direito.Tipo);
                            }
                        }
                    }
                }
            }

            return lista;
        }

        // ------------------------------------------------------------------------------------
        // Fiadas e juntas
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Alturas das fiadas. A junta horizontal entre duas fiadas e abaixada, quando preciso,
        /// ate ficar a distanciaMinima das arestas (topo e base) das aberturas.
        /// </summary>
        private static List<double[]> CalcularFiadas(
            double altura, double piso, double gap, double alturaFiada, double minPeca,
            IList<RetanguloMm> aberturas, double distanciaMinima)
        {
            var fiadas = new List<double[]>();
            double z0 = piso;
            int guarda = 0;

            while (z0 < altura - Eps && guarda < 100)
            {
                guarda++;
                double z1 = Math.Min(z0 + alturaFiada, altura);

                if (z1 < altura - Eps)
                {
                    z1 = AfastarJuntaHorizontal(z1, z0 + minPeca, aberturas, distanciaMinima);
                }

                fiadas.Add(new[] { z0, z1 });
                z0 = z1 + gap;
            }

            // Ultima fiada muito baixa: pega emprestado da anterior para nao sobrar uma tira.
            if (fiadas.Count >= 2)
            {
                double[] ultima = fiadas[fiadas.Count - 1];
                double[] anterior = fiadas[fiadas.Count - 2];

                if (ultima[1] - ultima[0] < minPeca)
                {
                    double novoFim = altura - minPeca - gap;
                    if (novoFim - anterior[0] >= minPeca)
                    {
                        anterior[1] = novoFim;
                        ultima[0] = novoFim + gap;
                    }
                }
            }

            return fiadas;
        }

        /// <summary>
        /// Desce a altura z (de 5 em 5 mm, ate minimoZ) ate achar uma posicao a pelo menos
        /// distanciaMinima do topo e da base de todas as aberturas. Se nao achar, mantem z.
        /// </summary>
        private static double AfastarJuntaHorizontal(
            double z, double minimoZ, IList<RetanguloMm> aberturas, double distanciaMinima)
        {
            if (distanciaMinima <= 0 || aberturas == null || aberturas.Count == 0) return z;

            for (double candidato = z; candidato >= minimoZ; candidato -= 5.0)
            {
                bool livre = true;
                foreach (RetanguloMm abertura in aberturas)
                {
                    if (Math.Abs(candidato - abertura.Z0) < distanciaMinima - Eps
                        || Math.Abs(candidato - abertura.Z1) < distanciaMinima - Eps)
                    {
                        livre = false;
                        break;
                    }
                }

                if (livre) return candidato;
            }

            return z;
        }

        /// <summary>
        /// Montantes que podem receber junta: os que ficam a pelo menos distanciaMinima da linha
        /// vertical de todas as quinas das aberturas, em qualquer altura.
        /// </summary>
        private static List<double> MontantesAfastadosDosVertices(
            List<double> montantes, List<double> verticesU, double distanciaMinima)
        {
            if (distanciaMinima <= 0 || verticesU.Count == 0) return new List<double>(montantes);

            return montantes
                .Where(s => verticesU.All(v => Math.Abs(s - v) >= distanciaMinima - Eps))
                .ToList();
        }

        /// <summary>
        /// Faixas {inicio, fim, centro} onde a junta e bem-vinda: o meio das aberturas que a fiada
        /// atravessa (a junta cai dentro do vao, longe das quinas e a placa fica em L).
        /// </summary>
        private static List<double[]> ZonasPreferidas(
            IList<RetanguloMm> aberturas, double z0, double z1, double distanciaMinima)
        {
            var zonas = new List<double[]>();
            if (distanciaMinima <= 0) return zonas;

            foreach (RetanguloMm abertura in aberturas)
            {
                bool cruzaAFiada = abertura.Z1 > z0 + Eps && abertura.Z0 < z1 - Eps;
                if (!cruzaAFiada) continue;
                if (abertura.Largura < 2.0 * distanciaMinima - Eps) continue;

                zonas.Add(new[]
                {
                    abertura.U0 + distanciaMinima,
                    abertura.U1 - distanciaMinima,
                    (abertura.U0 + abertura.U1) / 2.0
                });
            }

            return zonas;
        }

        /// <summary>
        /// Posicao da primeira junta de uma fiada deslocada: o montante mais proximo de meia
        /// placa, de preferencia desencontrado das juntas da fiada de baixo e sem deixar uma
        /// primeira peca menor que a largura minima.
        /// </summary>
        private static double DeslocamentoInicial(
            List<double> elegiveis, double span, double gap, double larguraPainel,
            List<double> juntasAnteriores, double minLargura)
        {
            double alvo = span / 2.0;

            List<double> candidatos = elegiveis
                .Where(s => s >= minLargura - Eps && s < larguraPainel - Eps && s <= span + gap + Eps)
                .ToList();
            if (candidatos.Count == 0)
            {
                candidatos = elegiveis
                    .Where(s => s > Eps && s < larguraPainel - Eps && s <= span + gap + Eps)
                    .ToList();
            }

            if (candidatos.Count == 0) return alvo;

            List<double> usar = candidatos;
            if (juntasAnteriores != null && juntasAnteriores.Count > 0)
            {
                List<double> afastados = candidatos
                    .Where(s => juntasAnteriores.All(j => Math.Abs(j - s) >= LimiteDesencontroMm - Eps))
                    .ToList();
                if (afastados.Count > 0) usar = afastados;
            }

            double melhor = double.NaN;
            foreach (double s in usar)
            {
                if (double.IsNaN(melhor) || Math.Abs(s - alvo) < Math.Abs(melhor - alvo) - 1e-6)
                {
                    melhor = s;
                }
            }

            return melhor;
        }

        /// <summary>
        /// Posicoes das juntas verticais de uma fiada, assentando a partir do inicio. Cada junta
        /// cai num montante; a placa avanca o maximo que cabe. Com deslocamento inicial, a primeira
        /// peca e mais curta (desencontro). Prefere montantes afastados das quinas das aberturas e,
        /// quando ha um bem no meio de uma abertura, usa esse. No fim, se a ultima peca ficaria
        /// menor que minLargura, recua a ultima (ou as duas ultimas) juntas.
        /// </summary>
        private static List<double> CalcularJuntasDaFiada(
            double larguraPainel, double span, double gap,
            List<double> montantesElegiveis, List<double> montantes, List<double[]> zonasPreferidas,
            double deslocamentoInicial, double minLargura, List<double> juntasAnteriores,
            out bool foraDeMontante, out bool pertoDeVertice)
        {
            foraDeMontante = false;
            pertoDeVertice = false;
            var juntas = new List<double>();
            double atual = 0.0;
            bool primeira = true;
            int guarda = 0;

            while (larguraPainel - atual > span + Eps && guarda < 1000)
            {
                guarda++;
                double proxima;

                if (primeira && deslocamentoInicial > Eps)
                {
                    proxima = deslocamentoInicial;
                }
                else
                {
                    double limite = atual + span + gap;

                    proxima = EscolherJunta(
                        montantesElegiveis, atual, limite, larguraPainel, span, minLargura,
                        juntasAnteriores, zonasPreferidas, true);

                    if (double.IsNaN(proxima))
                    {
                        proxima = EscolherJunta(
                            montantes, atual, limite, larguraPainel, span, minLargura,
                            juntasAnteriores, zonasPreferidas, true);
                        if (!double.IsNaN(proxima)) pertoDeVertice = true;
                    }

                    if (double.IsNaN(proxima))
                    {
                        proxima = EscolherJunta(
                            montantesElegiveis, atual, limite, larguraPainel, span, minLargura,
                            juntasAnteriores, zonasPreferidas, false);
                    }

                    if (double.IsNaN(proxima))
                    {
                        proxima = EscolherJunta(
                            montantes, atual, limite, larguraPainel, span, minLargura,
                            juntasAnteriores, zonasPreferidas, false);
                        if (!double.IsNaN(proxima)) pertoDeVertice = true;
                    }

                    if (double.IsNaN(proxima))
                    {
                        proxima = limite;
                        foraDeMontante = true;
                    }
                }

                primeira = false;
                juntas.Add(proxima);
                atual = proxima;
            }

            // Ultima peca menor que o minimo: recua a ultima junta (ou as duas ultimas).
            bool pertoNoFim;
            AjustarUltimaPeca(juntas, larguraPainel, span, gap, montantesElegiveis, montantes, minLargura, out pertoNoFim);
            if (pertoNoFim) pertoDeVertice = true;

            return juntas;
        }

        /// <summary>
        /// Se a peca do fim da fiada ficaria menor que minLargura, move a ultima junta para outro
        /// montante, de modo que a ultima peca e a anterior tenham pelo menos minLargura e caibam
        /// numa placa. Tenta primeiro os montantes afastados das quinas das aberturas e depois todos.
        /// Se uma junta so nao resolve, move as duas ultimas. Se nada serve, deixa como esta.
        /// </summary>
        private static void AjustarUltimaPeca(
            List<double> juntas, double larguraPainel, double span, double gap,
            List<double> montantesElegiveis, List<double> montantes, double minLargura,
            out bool usouMontanteProximoDeVertice)
        {
            usouMontanteProximoDeVertice = false;

            int n = juntas.Count;
            if (n == 0) return;
            if (larguraPainel - juntas[n - 1] >= minLargura - Eps) return;

            double larguraMaxima = span + gap;
            double anterior = n > 1 ? juntas[n - 2] : 0.0;

            double nova = MelhorUltimaJunta(montantesElegiveis, anterior, larguraPainel, minLargura, larguraMaxima);
            bool usouTodos = false;
            if (double.IsNaN(nova))
            {
                nova = MelhorUltimaJunta(montantes, anterior, larguraPainel, minLargura, larguraMaxima);
                usouTodos = !double.IsNaN(nova);
            }

            if (!double.IsNaN(nova))
            {
                juntas[n - 1] = nova;
                usouMontanteProximoDeVertice = usouTodos;
                return;
            }

            if (n < 2) return;

            // Duas juntas: a penultima tambem recua.
            double anterior2 = n > 2 ? juntas[n - 3] : 0.0;

            foreach (List<double> lista in new[] { montantesElegiveis, montantes })
            {
                for (int i = lista.Count - 1; i >= 0; i--)
                {
                    double a = lista[i];

                    if (a >= larguraPainel - Eps) continue;
                    if (a < anterior2 + minLargura - Eps) break;
                    if (a - anterior2 > larguraMaxima + Eps) continue;

                    double ultima = MelhorUltimaJunta(lista, a, larguraPainel, minLargura, larguraMaxima);
                    if (double.IsNaN(ultima)) continue;

                    juntas[n - 2] = a;
                    juntas[n - 1] = ultima;
                    usouMontanteProximoDeVertice = ReferenceEquals(lista, montantes);
                    return;
                }
            }
        }

        /// <summary>
        /// O maior montante para a ultima junta, de modo que a peca de tras (desde anterior) e a
        /// ultima peca tenham pelo menos minLargura e cada uma caiba numa placa. NaN se nao houver.
        /// </summary>
        private static double MelhorUltimaJunta(
            List<double> montantes, double anterior, double larguraPainel, double minLargura, double larguraMaxima)
        {
            double melhor = double.NaN;

            foreach (double s in montantes)
            {
                if (s >= larguraPainel - Eps) continue;
                if (s < anterior + minLargura - Eps) continue;
                if (s > larguraPainel - minLargura + Eps) continue;
                if (s - anterior > larguraMaxima + Eps) continue;
                if (larguraPainel - s > larguraMaxima + Eps) continue;

                if (double.IsNaN(melhor) || s > melhor) melhor = s;
            }

            return melhor;
        }

        /// <summary>
        /// Assenta a fiada tambem a partir do fim (espelhando o painel) e junta as duas: algumas
        /// juntas vindas do inicio e outras vindas do fim. Escolhe a combinacao com menos juntas
        /// em que a peca cortada do meio cabe numa placa e as pecas das pontas e do meio respeitam
        /// a largura minima; entre as de mesma quantidade, a que deixa maior a menor das duas pecas
        /// das pontas. Se nao achar com minLargura, tenta de novo com minAbsoluto. Devolve nulo se
        /// nao achar combinacao.
        /// </summary>
        private static List<double> JuntasAncoradasNasDuasPontas(
            List<double> daEsquerda, double larguraPainel, double span, double gap,
            List<double> elegiveis, List<double> montantes, List<double[]> zonas,
            bool desloca, double minLargura, double minAbsoluto, List<double> anteriores)
        {
            if (daEsquerda.Count == 0) return null;

            List<double> elegiveisE = Espelhar(elegiveis, larguraPainel);
            List<double> montantesE = Espelhar(montantes, larguraPainel);
            List<double[]> zonasE = zonas
                .Select(z => new[] { larguraPainel - z[1], larguraPainel - z[0], larguraPainel - z[2] })
                .ToList();
            List<double> anterioresE = anteriores == null ? null : Espelhar(anteriores, larguraPainel);

            double deslocamentoE = desloca
                ? DeslocamentoInicial(elegiveisE, span, gap, larguraPainel, anterioresE, minLargura)
                : 0.0;

            bool foraE;
            bool pertoE;
            List<double> daDireitaE = CalcularJuntasDaFiada(
                larguraPainel, span, gap, elegiveisE, montantesE, zonasE,
                deslocamentoE, minLargura, anterioresE, out foraE, out pertoE);
            List<double> daDireita = Espelhar(daDireitaE, larguraPainel);

            foreach (double minimo in new[] { minLargura, Math.Min(minLargura, minAbsoluto) })
            {
                List<double> melhor = null;
                int melhorQuantidade = int.MaxValue;
                double melhorCanto = -1.0;

                for (int k = 0; k <= daEsquerda.Count; k++)
                {
                    for (int m = 0; m <= daDireita.Count; m++)
                    {
                        double ultimaEsquerda = k > 0 ? daEsquerda[k - 1] : 0.0;
                        double primeiraDireita = m > 0 ? daDireita[daDireita.Count - m] : larguraPainel;
                        double meio = primeiraDireita - ultimaEsquerda;

                        // A peca entre as duas partes tem que caber numa placa e nao ser uma tira.
                        if (meio > span + gap + Eps) continue;
                        if (k + m > 0 && meio < minimo - Eps) continue;

                        double cantoEsquerdo = k > 0 ? daEsquerda[0] : meio;
                        double cantoDireito = m > 0 ? larguraPainel - daDireita[daDireita.Count - 1] : meio;

                        // As pecas das duas pontas tambem nao podem ser menores que o minimo.
                        if (k > 0 && cantoEsquerdo < minimo - Eps) continue;
                        if (m > 0 && cantoDireito < minimo - Eps) continue;

                        double canto = Math.Min(cantoEsquerdo, cantoDireito);
                        int quantidade = k + m;

                        if (quantidade < melhorQuantidade
                            || (quantidade == melhorQuantidade && canto > melhorCanto + 1e-6))
                        {
                            melhor = daEsquerda.Take(k).Concat(daDireita.Skip(daDireita.Count - m)).ToList();
                            melhorQuantidade = quantidade;
                            melhorCanto = canto;
                        }
                    }
                }

                if (melhor != null) return melhor;
            }

            return null;
        }

        /// <summary>Posicoes vistas do outro lado do painel (x vira largura - x), em ordem crescente.</summary>
        private static List<double> Espelhar(List<double> valores, double largura)
        {
            return valores.Select(v => largura - v).OrderBy(v => v).ToList();
        }

        /// <summary>
        /// Escolhe o montante da proxima junta entre os candidatos que a placa alcanca. Entre os
        /// viaveis, prefere os desencontrados das juntas da fiada de baixo; dentro deles, prefere
        /// o que cai no meio de uma abertura (mais perto do centro); senao, o mais distante.
        /// NaN se nenhum candidato serve.
        /// </summary>
        private static double EscolherJunta(
            List<double> candidatos, double atual, double limite, double larguraPainel,
            double span, double minLargura, List<double> juntasAnteriores, List<double[]> zonasPreferidas,
            bool exigirLarguraMinima)
        {
            var viaveis = new List<double>();
            foreach (double s in candidatos)
            {
                bool depois = exigirLarguraMinima ? s >= atual + minLargura - Eps : s > atual + Eps;
                if (!depois) continue;
                if (s >= larguraPainel - Eps) continue;
                if (s > limite + Eps) continue;

                viaveis.Add(s);
            }

            if (viaveis.Count == 0) return double.NaN;

            List<double> grupo = viaveis;
            if (juntasAnteriores != null && juntasAnteriores.Count > 0)
            {
                List<double> afastados = viaveis
                    .Where(s => juntasAnteriores.All(j => Math.Abs(j - s) >= LimiteDesencontroMm - Eps))
                    .ToList();
                if (afastados.Count > 0) grupo = afastados;
            }

            double melhor = double.NaN;
            double melhorDistancia = double.PositiveInfinity;
            foreach (double s in grupo)
            {
                if (s < atual + span / 2.0 - Eps) continue;

                foreach (double[] zona in zonasPreferidas)
                {
                    if (s < zona[0] - Eps || s > zona[1] + Eps) continue;

                    double d = Math.Abs(s - zona[2]);
                    if (d < melhorDistancia)
                    {
                        melhorDistancia = d;
                        melhor = s;
                    }
                }
            }

            if (!double.IsNaN(melhor)) return melhor;

            return grupo.Max();
        }

        // ------------------------------------------------------------------------------------
        // Recortes
        // ------------------------------------------------------------------------------------

        private sealed class Componente
        {
            public RetanguloMm Contorno;
            public readonly List<RetanguloMm> Celulas = new List<RetanguloMm>();
        }

        /// <summary>
        /// Tira as aberturas de um retangulo de placa. Se as aberturas deixam a placa em pedacos
        /// soltos, devolve um componente por pedaco; se so fazem recortes (L, U ou furo), devolve
        /// um unico componente.
        /// </summary>
        private static List<Componente> SepararPorAberturas(RetanguloMm b, IList<RetanguloMm> aberturas)
        {
            var resultado = new List<Componente>();

            List<RetanguloMm> relevantes = aberturas
                .Where(a => a.U1 > b.U0 + Eps && a.U0 < b.U1 - Eps && a.Z1 > b.Z0 + Eps && a.Z0 < b.Z1 - Eps)
                .ToList();

            if (relevantes.Count == 0)
            {
                var unico = new Componente { Contorno = b };
                unico.Celulas.Add(b);
                resultado.Add(unico);
                return resultado;
            }

            var xsBrutos = new List<double> { b.U0, b.U1 };
            var zsBrutos = new List<double> { b.Z0, b.Z1 };

            foreach (RetanguloMm a in relevantes)
            {
                xsBrutos.Add(Math.Max(b.U0, Math.Min(b.U1, a.U0)));
                xsBrutos.Add(Math.Max(b.U0, Math.Min(b.U1, a.U1)));
                zsBrutos.Add(Math.Max(b.Z0, Math.Min(b.Z1, a.Z0)));
                zsBrutos.Add(Math.Max(b.Z0, Math.Min(b.Z1, a.Z1)));
            }

            List<double> xs = Unificar(xsBrutos);
            List<double> zs = Unificar(zsBrutos);
            int nx = xs.Count - 1;
            int nz = zs.Count - 1;

            var livre = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    double cx = (xs[i] + xs[i + 1]) / 2.0;
                    double cz = (zs[j] + zs[j + 1]) / 2.0;
                    livre[i, j] = !relevantes.Any(a => cx > a.U0 && cx < a.U1 && cz > a.Z0 && cz < a.Z1);
                }
            }

            var visitado = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!livre[i, j] || visitado[i, j]) continue;

                    var indices = new List<int[]>();
                    var pilha = new Stack<int[]>();
                    pilha.Push(new[] { i, j });
                    visitado[i, j] = true;

                    while (pilha.Count > 0)
                    {
                        int[] atualIdx = pilha.Pop();
                        indices.Add(atualIdx);

                        int[][] vizinhos =
                        {
                            new[] { atualIdx[0] + 1, atualIdx[1] },
                            new[] { atualIdx[0] - 1, atualIdx[1] },
                            new[] { atualIdx[0], atualIdx[1] + 1 },
                            new[] { atualIdx[0], atualIdx[1] - 1 }
                        };

                        foreach (int[] viz in vizinhos)
                        {
                            if (viz[0] < 0 || viz[0] >= nx || viz[1] < 0 || viz[1] >= nz) continue;
                            if (!livre[viz[0], viz[1]] || visitado[viz[0], viz[1]]) continue;

                            visitado[viz[0], viz[1]] = true;
                            pilha.Push(viz);
                        }
                    }

                    List<RetanguloMm> retangulos = indices
                        .Select(idx => new RetanguloMm(xs[idx[0]], xs[idx[0] + 1], zs[idx[1]], zs[idx[1] + 1]))
                        .ToList();

                    var componente = new Componente();
                    componente.Celulas.AddRange(MesclarCelulas(retangulos));
                    componente.Contorno = new RetanguloMm(
                        retangulos.Min(q => q.U0),
                        retangulos.Max(q => q.U1),
                        retangulos.Min(q => q.Z0),
                        retangulos.Max(q => q.Z1));
                    resultado.Add(componente);
                }
            }

            return resultado;
        }

        private static List<double> Unificar(List<double> valores)
        {
            var resultado = new List<double>();

            foreach (double v in valores.OrderBy(x => x))
            {
                if (resultado.Count == 0 || v - resultado[resultado.Count - 1] > ToleranciaCelula)
                {
                    resultado.Add(v);
                }
            }

            return resultado;
        }

        private static string Chave(double valor)
        {
            return Math.Round(valor * 10.0).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Junta celulas vizinhas em retangulos maiores (primeiro na horizontal, depois na vertical).</summary>
        private static List<RetanguloMm> MesclarCelulas(List<RetanguloMm> celulas)
        {
            var horizontais = new List<RetanguloMm>();

            foreach (var faixa in celulas.GroupBy(cel => Chave(cel.Z0) + "|" + Chave(cel.Z1)))
            {
                RetanguloMm corrente = null;

                foreach (RetanguloMm cel in faixa.OrderBy(x => x.U0))
                {
                    if (corrente != null && Math.Abs(corrente.U1 - cel.U0) < ToleranciaCelula)
                    {
                        corrente = new RetanguloMm(corrente.U0, cel.U1, corrente.Z0, corrente.Z1);
                    }
                    else
                    {
                        if (corrente != null) horizontais.Add(corrente);
                        corrente = cel;
                    }
                }

                if (corrente != null) horizontais.Add(corrente);
            }

            var resultado = new List<RetanguloMm>();

            foreach (var coluna in horizontais.GroupBy(cel => Chave(cel.U0) + "|" + Chave(cel.U1)))
            {
                RetanguloMm corrente = null;

                foreach (RetanguloMm cel in coluna.OrderBy(x => x.Z0))
                {
                    if (corrente != null && Math.Abs(corrente.Z1 - cel.Z0) < ToleranciaCelula)
                    {
                        corrente = new RetanguloMm(corrente.U0, corrente.U1, corrente.Z0, cel.Z1);
                    }
                    else
                    {
                        if (corrente != null) resultado.Add(corrente);
                        corrente = cel;
                    }
                }

                if (corrente != null) resultado.Add(corrente);
            }

            return resultado;
        }

        private static bool TocaBorda(RetanguloMm recorte, RetanguloMm contorno)
        {
            return Math.Abs(recorte.U0 - contorno.U0) <= Eps
                || Math.Abs(recorte.U1 - contorno.U1) <= Eps
                || Math.Abs(recorte.Z0 - contorno.Z0) <= Eps
                || Math.Abs(recorte.Z1 - contorno.Z1) <= Eps;
        }

        // ------------------------------------------------------------------------------------
        // Juntas (metros de rejunte)
        // ------------------------------------------------------------------------------------

        private static void CalcularJuntasInternas(ResultadoPaginacao resultado, double gap)
        {
            List<PecaPlaca> pecas = resultado.Pecas;

            for (int i = 0; i < pecas.Count; i++)
            {
                for (int j = i + 1; j < pecas.Count; j++)
                {
                    foreach (RetanguloMm a in pecas[i].Celulas)
                    {
                        foreach (RetanguloMm b in pecas[j].Celulas)
                        {
                            double sobreposicaoZ = Math.Min(a.Z1, b.Z1) - Math.Max(a.Z0, b.Z0);
                            double sobreposicaoU = Math.Min(a.U1, b.U1) - Math.Max(a.U0, b.U0);

                            bool lado = Math.Abs((a.U1 + gap) - b.U0) <= ToleranciaJunta
                                || Math.Abs((b.U1 + gap) - a.U0) <= ToleranciaJunta;
                            if (lado && sobreposicaoZ > ToleranciaJunta)
                            {
                                Acumular(resultado.Juntas, pecas[i], pecas[j], true, sobreposicaoZ, resultado.Tipo, resultado.Tipo);
                            }

                            bool topo = Math.Abs((a.Z1 + gap) - b.Z0) <= ToleranciaJunta
                                || Math.Abs((b.Z1 + gap) - a.Z0) <= ToleranciaJunta;
                            if (topo && sobreposicaoU > ToleranciaJunta)
                            {
                                Acumular(resultado.Juntas, pecas[i], pecas[j], false, sobreposicaoU, resultado.Tipo, resultado.Tipo);
                            }
                        }
                    }
                }
            }
        }

        private static void Acumular(
            List<JuntaPlaca> lista, PecaPlaca a, PecaPlaca b, bool vertical, double comprimento,
            TipoPlaca tipoA, TipoPlaca tipoB)
        {
            JuntaPlaca existente = lista.FirstOrDefault(
                j => j.CodigoA == a.Codigo && j.CodigoB == b.Codigo && j.Vertical == vertical);

            if (existente != null)
            {
                existente.ComprimentoMm += comprimento;
                return;
            }

            lista.Add(new JuntaPlaca
            {
                CodigoA = a.Codigo,
                CodigoB = b.Codigo,
                TipoA = tipoA,
                TipoB = tipoB,
                Vertical = vertical,
                ComprimentoMm = comprimento
            });
        }
    }
}