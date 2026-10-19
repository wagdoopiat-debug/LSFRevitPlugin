using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;

namespace LSFRevitPlugin.Services
{
    /// <summary>Uma tabela do quantitativo (vira uma aba da janela e uma secao do CSV).</summary>
    public sealed class SecaoQuantitativo
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        /// <summary>Nome curto da aba na janela.</summary>
        public string Aba { get; set; }

        /// <summary>Titulo da secao no CSV.</summary>
        public string Titulo { get; set; }

        /// <summary>Cabecalho das colunas; nulo se a secao nao tem tabela.</summary>
        public string[] Colunas { get; set; }

        public List<string[]> Linhas { get; } = new List<string[]>();

        /// <summary>true = aparece so no CSV, nao na janela.</summary>
        public bool SoNoCsv { get; set; }

        /// <summary>true = a ultima coluna (texto longo) ocupa o espaco que sobra na janela.</summary>
        public bool UltimaColunaLarga { get; set; }

        /// <summary>Adiciona uma linha; completa com vazio se faltarem campos.</summary>
        public void Adicionar(params object[] campos)
        {
            int total = Colunas != null ? Math.Max(Colunas.Length, campos.Length) : campos.Length;
            var linha = new string[total];

            for (int i = 0; i < total; i++)
            {
                linha[i] = i < campos.Length ? (Convert.ToString(campos[i], PtBr) ?? "") : "";
            }

            Linhas.Add(linha);
        }
    }

    /// <summary>Tudo o que o quantitativo calculou do projeto, pronto para mostrar na janela ou gravar em CSV.</summary>
    public sealed class DadosQuantitativo
    {
        public string Projeto { get; set; }
        public string DataHora { get; set; }
        public int ElementosEstrutura { get; set; }
        public double ComprimentoTotalM { get; set; }
        public int PecasDePlaca { get; set; }
        public int PlacasUsadas { get; set; }
        public int Recortes { get; set; }

        /// <summary>Parafusos da estrutura (montante na guia, emendas e conexoes).</summary>
        public int ParafusosEstrutura { get; set; }

        /// <summary>Parafusos das placas.</summary>
        public int ParafusosPlacas { get; set; }

        /// <summary>Posicao de cada parafuso (usada para criar os parafusos no 3D).</summary>
        public ResultadoParafusos Parafusos { get; set; }

        /// <summary>Frase sobre o que o quantitativo ainda nao inclui.</summary>
        public string Observacao { get; set; } = "Ainda não inclui o rejunte.";

        public List<SecaoQuantitativo> Secoes { get; } = new List<SecaoQuantitativo>();
        public List<string> Avisos { get; } = new List<string>();
    }

    /// <summary>
    /// Quantitativo do projeto, lido do que ja esta modelado (estrutura e placas). Calcular monta as
    /// tabelas (para a janela); GravarCsv grava as mesmas tabelas num CSV que o Excel abre, com a
    /// legenda dos codigos. Inclui os parafusos da estrutura e das placas; ainda nao inclui rejunte.
    /// </summary>
    public static class QuantitativoService
    {
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

        private static readonly Regex PadraoRecorte = new Regex(
            @"^(?<cod>\S+-R\d+)\s+(?<l>\d+(?:[.,]\d+)?)\s*x\s*(?<a>\d+(?:[.,]\d+)?)\s*mm$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PadraoMedida = new Regex(
            @"^\d+(?:[.,]\d+)?\s*x\s*\d+(?:[.,]\d+)?\s*mm$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Tipos de elemento gravados pela geracao da estrutura no campo Comentarios.
        private static readonly Dictionary<string, string> DescricaoDosTiposDeEstrutura =
            new Dictionary<string, string>
            {
                { "Montante principal", "Montante da grade (espacamento do projeto) e montantes de extremidade da parede." },
                { "Montante de divisa de painel", "Montante de extremidade de cada lado da divisa entre dois paineis." },
                { "Montante H (canto em L)", "Par de montantes costas com costas do canto em L." },
                { "Montante Triplo (encontro em T)", "Montante do encontro em T, na parede que chega." },
                { "Montante T (parede passante)", "Montantes do encontro em T, na parede que passa." },
                { "Montante Quadruplo (encontro em +)", "Montantes do encontro em +." },
                { "Reforco de vao (King/Jack/verga/contra-verga)", "King, jack, verga e contra-verga das aberturas." },
                { "Cripple stud", "Montante curto acima ou abaixo de uma abertura." },
                { "Guia inferior", "Perfil U no piso; e interrompido nas portas." },
                { "Guia superior", "Perfil U no topo da parede." }
            };

        private sealed class RecorteLido
        {
            public string Codigo;
            public double LarguraMm;
            public double AlturaMm;
        }

        private sealed class DetalheDaPeca
        {
            public string Medida = "";
            public readonly List<RecorteLido> Recortes = new List<RecorteLido>();
        }

        /// <summary>Le o projeto e monta as tabelas do quantitativo. Nao grava nada.</summary>
        public static DadosQuantitativo Calcular(Document doc)
        {
            var dados = new DadosQuantitativo
            {
                Projeto = doc.Title,
                DataHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Pt)
            };

            List<ElementoEstruturaLido> estrutura = LeitorDeEstrutura.Ler(doc, dados.Avisos);
            List<PlacaExistente> placas = PlacasExistentes.Ler(doc).Todas.ToList();

            if (estrutura.Count == 0 && placas.Count == 0)
            {
                dados.Avisos.Add("Nenhum elemento de estrutura ou de placa do LSF foi encontrado neste projeto.");
            }

            var detalhes = new Dictionary<PlacaExistente, DetalheDaPeca>();
            foreach (PlacaExistente p in placas)
            {
                detalhes[p] = LerDetalhe(p);
            }

            AdicionarEstrutura(dados, estrutura);
            int placasUsadas = AdicionarPlacas(dados, placas, detalhes);

            ResultadoParafusos parafusos = ParafusosService.Calcular(doc, estrutura, placas, dados.Avisos);
            dados.Parafusos = parafusos;
            AdicionarParafusos(dados, parafusos, estrutura, placas);

            AdicionarLegenda(dados, estrutura, placas);

            dados.ElementosEstrutura = estrutura.Count;
            dados.ComprimentoTotalM = estrutura.Sum(e => e.ComprimentoMm) / 1000.0;
            dados.PecasDePlaca = placas.Count;
            dados.PlacasUsadas = placasUsadas;
            dados.Recortes = detalhes.Values.Sum(d => d.Recortes.Count);
            return dados;
        }

        /// <summary>Grava as tabelas calculadas num CSV (separador ponto e virgula) na pasta dada e devolve o caminho.</summary>
        public static string GravarCsv(DadosQuantitativo dados, string pasta)
        {
            var csv = new StringBuilder();
            csv.AppendLine(Linha("LSF Modeler - Quantitativo"));
            csv.AppendLine(Linha("Projeto", dados.Projeto));
            csv.AppendLine(Linha("Data", dados.DataHora));
            csv.AppendLine();

            foreach (SecaoQuantitativo secao in dados.Secoes)
            {
                csv.AppendLine(Linha(secao.Titulo));
                if (secao.Colunas != null) csv.AppendLine(Linha((object[])secao.Colunas));

                foreach (string[] linha in secao.Linhas)
                {
                    csv.AppendLine(Linha((object[])linha));
                }

                csv.AppendLine();
            }

            string caminho = Path.Combine(pasta, "LSF_Quantitativo_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv");
            File.WriteAllText(caminho, csv.ToString(), new UTF8Encoding(true));
            return caminho;
        }

        // ------------------------------------------------------------------------------------
        // Tabelas
        // ------------------------------------------------------------------------------------

        private static void AdicionarEstrutura(DadosQuantitativo dados, List<ElementoEstruturaLido> estrutura)
        {
            var resumo = new SecaoQuantitativo
            {
                Aba = "Estrutura",
                Titulo = "ESTRUTURA - RESUMO",
                Colunas = new[] { "Tipo", "Perfil", "Quantidade", "Comprimento total (m)" }
            };

            foreach (var g in estrutura
                .GroupBy(e => new { e.Tipo, e.Perfil })
                .OrderBy(g => g.Key.Tipo).ThenBy(g => g.Key.Perfil))
            {
                resumo.Adicionar(g.Key.Tipo, g.Key.Perfil, g.Count(), N(g.Sum(e => e.ComprimentoMm) / 1000.0));
            }

            resumo.Adicionar("TOTAL", "", estrutura.Count, N(estrutura.Sum(e => e.ComprimentoMm) / 1000.0));
            dados.Secoes.Add(resumo);

            var corte = new SecaoQuantitativo
            {
                Aba = "Corte",
                Titulo = "ESTRUTURA - LISTA DE CORTE",
                Colunas = new[] { "Tipo", "Perfil", "Comprimento (mm)", "Quantidade" }
            };

            foreach (var g in estrutura
                .GroupBy(e => new { e.Tipo, e.Perfil, Comprimento = Math.Round(e.ComprimentoMm) })
                .OrderBy(g => g.Key.Tipo).ThenBy(g => g.Key.Perfil).ThenByDescending(g => g.Key.Comprimento))
            {
                corte.Adicionar(g.Key.Tipo, g.Key.Perfil, g.Key.Comprimento.ToString("0", Pt), g.Count());
            }

            dados.Secoes.Add(corte);
        }

        /// <summary>Monta as tabelas de placas e devolve o total de placas de fabrica usadas.</summary>
        private static int AdicionarPlacas(
            DadosQuantitativo dados, List<PlacaExistente> placas, Dictionary<PlacaExistente, DetalheDaPeca> detalhes)
        {
            // ---- Resumo ----
            var resumo = new SecaoQuantitativo
            {
                Aba = "Placas",
                Titulo = "PLACAS - RESUMO",
                Colunas = new[]
                {
                    "Tipo", "Medida", "Placas usadas", "Pecas", "Area liquida (m2)",
                    "Area das placas (m2)", "Aproveitamento (%)"
                }
            };

            int placasUsadas = 0;
            foreach (var g in placas
                .GroupBy(p => new
                {
                    Tipo = p.Tipo.HasValue ? PlacaCatalogo.NomeDoTipo(p.Tipo.Value) : "(desconhecido)",
                    Medida = p.Medida != null ? p.Medida.Descricao : "(desconhecida)"
                })
                .OrderBy(g => g.Key.Tipo).ThenBy(g => g.Key.Medida))
            {
                int folhas = g.Select(p => p.PlacaOrigem).Where(s => s.Length > 0).Distinct().Count();
                placasUsadas += folhas;

                MedidaPlaca medida = g.Select(p => p.Medida).FirstOrDefault(m => m != null);
                double areaFolhas = medida != null ? folhas * medida.LadoCurtoMm * medida.LadoLongoMm / 1000000.0 : 0.0;
                double areaLiquida = g.Sum(p => p.AreaLiquidaMm2) / 1000000.0;
                string aproveitamento = areaFolhas > 0 ? N(areaLiquida / areaFolhas * 100.0, 1) : "";

                resumo.Adicionar(
                    g.Key.Tipo, g.Key.Medida, folhas, g.Count(), N(areaLiquida),
                    areaFolhas > 0 ? N(areaFolhas) : "", aproveitamento);
            }

            dados.Secoes.Add(resumo);

            List<PlacaExistente> ordenadas = placas
                .OrderBy(x => x.ParedeId).ThenBy(x => x.Painel).ThenBy(x => x.Face).ThenBy(x => x.Codigo)
                .ToList();

            // ---- Pecas ----
            var pecas = new SecaoQuantitativo
            {
                Aba = "Pecas",
                Titulo = "PLACAS - PECAS",
                Colunas = new[]
                {
                    "Codigo", "Parede", "Painel", "Face", "Tipo", "Medida da peca (mm)",
                    "Recortes (largura x altura, mm)", "Placa de origem", "Reaproveitada", "Area (m2)"
                }
            };

            foreach (PlacaExistente p in ordenadas)
            {
                DetalheDaPeca detalhe = detalhes[p];
                string recortes = string.Join("; ", detalhe.Recortes.Select(r =>
                    SufixoDoRecorte(r.Codigo) + " " + N(r.LarguraMm, 0) + " x " + N(r.AlturaMm, 0)));

                pecas.Adicionar(
                    p.Codigo, p.ParedeId, p.Painel, p.Face,
                    p.Tipo.HasValue ? PlacaCatalogo.NomeDoTipo(p.Tipo.Value) : "(desconhecido)",
                    detalhe.Medida, recortes,
                    p.PlacaOrigem, p.Reaproveitada ? "sim" : "nao", N(p.AreaLiquidaMm2 / 1000000.0, 3));
            }

            dados.Secoes.Add(pecas);

            // ---- Recortes ----
            var recortesSecao = new SecaoQuantitativo
            {
                Aba = "Recortes",
                Titulo = "PLACAS - RECORTES",
                Colunas = new[]
                {
                    "Recorte", "Peca", "Largura (mm)", "Altura (mm)", "Parede", "Painel", "Face", "Tipo"
                }
            };

            foreach (PlacaExistente p in ordenadas)
            {
                foreach (RecorteLido r in detalhes[p].Recortes)
                {
                    recortesSecao.Adicionar(
                        r.Codigo, p.Codigo, N(r.LarguraMm, 0), N(r.AlturaMm, 0),
                        p.ParedeId, p.Painel, p.Face,
                        p.Tipo.HasValue ? PlacaCatalogo.NomeDoTipo(p.Tipo.Value) : "(desconhecido)");
                }
            }

            dados.Secoes.Add(recortesSecao);

            return placasUsadas;
        }

        // ------------------------------------------------------------------------------------
        // Parafusos
        // ------------------------------------------------------------------------------------

        private static void AdicionarParafusos(
            DadosQuantitativo dados, ResultadoParafusos r,
            List<ElementoEstruturaLido> estrutura, List<PlacaExistente> placas)
        {
            Func<string, int> total = categoria => r.Parafusos.Count(p => p.Categoria == categoria);

            int naGuia = total(ParafusosService.CategoriaGuia);
            int costas = total(ParafusosService.CategoriaCostas);
            int entrePaineis = total(ParafusosService.CategoriaPaineis);
            int entreParedes = total(ParafusosService.CategoriaParedes);
            int totalEstrutura = naGuia + costas + entrePaineis + entreParedes;
            int totalPlacas = total(ParafusosService.CategoriaPlaca);

            dados.ParafusosEstrutura = totalEstrutura;
            dados.ParafusosPlacas = totalPlacas;

            // ---- Resumo ----
            var resumo = new SecaoQuantitativo
            {
                Aba = "Parafusos",
                Titulo = "PARAFUSOS - RESUMO",
                Colunas = new[] { "Ligacao", "Parafuso", "Ligacoes ou pecas (un)", "Parafusos (un)" }
            };

            resumo.Adicionar(
                "Montante na guia (" + ParafusosService.ParafusosPorMontanteNaGuia + " por montante)",
                ParafusosService.NomeParafusoEstrutura, r.Montantes, naGuia);
            resumo.Adicionar(
                ParafusosService.CategoriaCostas, ParafusosService.NomeParafusoEstrutura,
                r.Ligacoes.Count(l => l.Tipo == ParafusosService.CategoriaCostas), costas);
            resumo.Adicionar(
                ParafusosService.CategoriaPaineis, ParafusosService.NomeParafusoEstrutura,
                r.Ligacoes.Count(l => l.Tipo == ParafusosService.CategoriaPaineis), entrePaineis);
            resumo.Adicionar(
                ParafusosService.CategoriaParedes, ParafusosService.NomeParafusoEstrutura,
                r.Ligacoes.Count(l => l.Tipo == ParafusosService.CategoriaParedes), entreParedes);
            resumo.Adicionar("TOTAL (estrutura)", "", "", totalEstrutura);

            foreach (var g in r.Parafusos
                .Where(p => p.Categoria == ParafusosService.CategoriaPlaca)
                .GroupBy(p => new { p.Tipo, p.Descricao })
                .OrderBy(g => g.Key.Tipo))
            {
                resumo.Adicionar(
                    "Placas - " + g.Key.Tipo, g.Key.Descricao,
                    g.Select(p => p.Peca).Distinct().Count(), g.Count());
            }

            resumo.Adicionar("TOTAL (placas)", "", "", totalPlacas);
            resumo.Adicionar("TOTAL GERAL", "", "", totalEstrutura + totalPlacas);
            dados.Secoes.Add(resumo);

            // ---- Por painel ----
            var porPainel = new SecaoQuantitativo
            {
                Aba = "Parafusos por painel",
                Titulo = "PARAFUSOS - POR PAINEL",
                Colunas = new[]
                {
                    "Parede", "Painel", "Montante na guia", "Costas com costas",
                    "Entre paineis", "Entre paredes", "Placas", "Total"
                }
            };

            Dictionary<Tuple<int, int, string>, int> contagem = r.Parafusos
                .GroupBy(p => Tuple.Create(p.ParedeId, p.Painel, p.Categoria))
                .ToDictionary(g => g.Key, g => g.Count());

            Func<int, int, string, int> quanto = (parede, painel, categoria) =>
            {
                int valor;
                return contagem.TryGetValue(Tuple.Create(parede, painel, categoria), out valor) ? valor : 0;
            };

            List<Tuple<int, int>> chaves = estrutura.Select(e => Tuple.Create(e.ParedeId, e.Painel))
                .Concat(placas.Select(p => Tuple.Create(p.ParedeId, p.Painel)))
                .Distinct()
                .OrderBy(k => k.Item1).ThenBy(k => k.Item2)
                .ToList();

            foreach (Tuple<int, int> chave in chaves)
            {
                int g1 = quanto(chave.Item1, chave.Item2, ParafusosService.CategoriaGuia);
                int g2 = quanto(chave.Item1, chave.Item2, ParafusosService.CategoriaCostas);
                int g3 = quanto(chave.Item1, chave.Item2, ParafusosService.CategoriaPaineis);
                int g4 = quanto(chave.Item1, chave.Item2, ParafusosService.CategoriaParedes);
                int g5 = quanto(chave.Item1, chave.Item2, ParafusosService.CategoriaPlaca);

                porPainel.Adicionar(chave.Item1, chave.Item2, g1, g2, g3, g4, g5, g1 + g2 + g3 + g4 + g5);
            }

            porPainel.Adicionar(
                "TOTAL", "", naGuia, costas, entrePaineis, entreParedes, totalPlacas,
                totalEstrutura + totalPlacas);
            dados.Secoes.Add(porPainel);

            // ---- Parafusos das placas, por peca ----
            var porPeca = new SecaoQuantitativo
            {
                Aba = "Parafusos das placas",
                Titulo = "PARAFUSOS - PLACAS POR PECA",
                Colunas = new[] { "Peca", "Parede", "Painel", "Face", "Tipo", "Parafuso", "Parafusos (un)" }
            };

            Dictionary<string, int> porCodigo = r.Parafusos
                .Where(p => p.Categoria == ParafusosService.CategoriaPlaca)
                .GroupBy(p => p.Peca)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (PlacaExistente p in placas
                .OrderBy(x => x.ParedeId).ThenBy(x => x.Painel).ThenBy(x => x.Face).ThenBy(x => x.Codigo))
            {
                int quantidade;
                if (!porCodigo.TryGetValue(p.Codigo, out quantidade)) quantidade = 0;

                porPeca.Adicionar(
                    p.Codigo, p.ParedeId, p.Painel, p.Face,
                    p.Tipo.HasValue ? PlacaCatalogo.NomeDoTipo(p.Tipo.Value) : "(desconhecido)",
                    p.Tipo.HasValue ? PlacaCatalogo.RegraDoTipo(p.Tipo.Value).ParafusoPadrao : "",
                    quantidade);
            }

            dados.Secoes.Add(porPeca);

            // ---- Ligacoes entre montantes (para conferir) ----
            var lista = new SecaoQuantitativo
            {
                Aba = "Ligacoes",
                Titulo = "PARAFUSOS - LIGACOES ENTRE MONTANTES",
                Colunas = new[]
                {
                    "Ligacao", "Parede", "Painel", "Elemento A", "Elemento B",
                    "Altura de contato (mm)", "Parafusos"
                }
            };

            foreach (LigacaoMontantes l in r.Ligacoes
                .OrderBy(x => x.Dono.ParedeId).ThenBy(x => x.Dono.Painel)
                .ThenBy(x => x.Tipo).ThenBy(x => x.A.ElementoId))
            {
                lista.Adicionar(
                    l.Tipo, l.Dono.ParedeId, l.Dono.Painel,
                    l.A.Tipo + " #" + l.A.ElementoId + " (parede " + l.A.ParedeId + ", painel " + l.A.Painel + ")",
                    l.B.Tipo + " #" + l.B.ElementoId + " (parede " + l.B.ParedeId + ", painel " + l.B.Painel + ")",
                    N(l.AlturaMm, 0), l.Parafusos);
            }

            dados.Secoes.Add(lista);

            // ---- Criterios ----
            var criterios = new SecaoQuantitativo
            {
                Aba = "Criterios",
                Titulo = "PARAFUSOS - CRITERIOS",
                Colunas = new[] { "Criterio", "Valor" },
                UltimaColunaLarga = true
            };

            criterios.Adicionar("Parafuso da estrutura", ParafusosService.NomeParafusoEstrutura + ".");
            criterios.Adicionar(
                "Montante na guia",
                ParafusosService.ParafusosPorMontanteNaGuia + " parafusos por montante vertical: principal, de divisa de painel, H, "
                + "triplo, T, quadruplo, King, Jack e cripple stud. Verga e contra-verga (horizontais) e guias nao entram.");
            criterios.Adicionar(
                "Emendas e conexoes",
                "Para cada par de montantes verticais que se tocam: parafusos em zigue-zague ao longo da altura de contato, "
                + "passo de " + ParafusosService.PassoEmendaMm.ToString("0.#", Pt) + " mm (faixa de projeto 200 a 300 mm), a "
                + ParafusosService.BordaEmendaMm.ToString("0.#", Pt) + " mm das bordas (faixa de projeto 10 a 15 mm).");
            criterios.Adicionar(
                "Emenda costas com costas",
                "Dois montantes que se tocam no mesmo painel (pares do canto em L, do T e do +, King e Jack).");
            criterios.Adicionar(
                "Conexao entre paineis",
                "Dois montantes que se tocam na divisa de paineis da mesma parede.");
            criterios.Adicionar(
                "Conexao entre paredes",
                "Dois montantes de paredes diferentes que se tocam (cantos em L, T e +).");
            criterios.Adicionar(
                "Em qual painel a ligacao e contada",
                "Cada ligacao e contada uma so vez, no painel de menor numero (e na parede de menor Id) dos dois montantes.");
            criterios.Adicionar(
                "Parafusos das placas",
                "Uma linha de parafusos em cada montante da mesma parede que fica atras da peca (dentro da mesa do perfil, "
                + "onde a peca tem material, a pelo menos a distancia da borda das bordas), medida na geometria da peca, "
                + "ja descontando os recortes. Pecas apoiadas no piso ou no topo tambem recebem uma linha ao longo da guia. "
                + "Montantes de outras paredes (cantos e encontros) nao entram. Cripple stud so conta no trecho onde existe.");

            foreach (TipoPlaca tipo in PlacaCatalogo.TodosOsTipos())
            {
                RegraFixacaoPlaca regra = PlacaCatalogo.RegraDoTipo(tipo);
                criterios.Adicionar(
                    "Placa - " + PlacaCatalogo.NomeDoTipo(tipo),
                    regra.ParafusoPadrao + "; espacamento ao longo do apoio " + regra.EspacamentoAoLongoDoApoioMm.ToString("0", Pt)
                    + " mm; distancia da borda " + regra.DistanciaDaBordaMm.ToString("0", Pt) + " mm"
                    + (regra.PrecisaValidar ? " [VALIDAR com o fabricante]" : "") + ".");
            }

            criterios.Adicionar(
                "Como as ligacoes sao achadas",
                "Pela geometria dos elementos modelados. Se a estrutura ou as placas forem alteradas, calcule de novo.");

            dados.Secoes.Add(criterios);
        }

        // ------------------------------------------------------------------------------------
        // Legenda
        // ------------------------------------------------------------------------------------

        /// <summary>Legenda dos codigos usados nas placas, nos paineis e na estrutura, e a lista de paredes e paineis.</summary>
        private static void AdicionarLegenda(
            DadosQuantitativo dados, List<ElementoEstruturaLido> estrutura, List<PlacaExistente> placas)
        {
            var legenda = new SecaoQuantitativo
            {
                Aba = "Legenda",
                Titulo = "LEGENDA DOS CODIGOS",
                Colunas = new[] { "Codigo", "Significado" },
                UltimaColunaLarga = true
            };

            legenda.Adicionar(
                "<parede>-P<painel>-F<face>-<nn>",
                "Peça de placa. Ex.: 1234-P2-F1-03 = parede 1234 (Id do elemento no Revit), painel 2 dessa parede, "
                + "face 1, peça 03 (ordem de leitura: fiada de baixo para cima, da esquerda para a direita). "
                + "O mesmo código está no parâmetro Marca do elemento.");
            legenda.Adicionar(
                "<peça>-R<n>",
                "Recorte (abertura) dentro de uma peça. Ex.: 1234-P2-F1-03-R1 = primeiro recorte da peça; "
                + "largura x altura na tabela Recortes e no campo Comentários da peça.");
            legenda.Adicionar(
                "<sigla>-<nnn>",
                "Placa de fábrica de onde a peça é cortada. Ex.: GST-003 = terceira placa de gesso ST usada no projeto. "
                + "\"sobra de GST-001\" indica que a peça saiu de uma sobra dessa placa.");

            foreach (TipoPlaca tipo in PlacaCatalogo.TodosOsTipos())
            {
                legenda.Adicionar(
                    Paginador.CodigoCurto(tipo) + "-nnn",
                    "Placa de fábrica do tipo " + PlacaCatalogo.NomeDoTipo(tipo) + ".");
            }

            legenda.Adicionar("Face 1", "Lado da parede para onde a normal dela aponta (normalmente o exterior).");
            legenda.Adicionar("Face 2", "Lado oposto da parede.");
            legenda.Adicionar(
                "Painel <n>",
                "Divisão da parede criada em Painelizar, numerada a partir da ponta inicial da parede. "
                + "Sem painelização, a parede inteira é o painel 1.");
            legenda.Adicionar(
                "Parede <Id>", "Id do elemento parede no Revit; use Selecionar por Id para achá-la.");
            legenda.Adicionar(
                "LSF | Parede <Id> | Painel <n> | <tipo> | <perfil>",
                "Texto do campo Comentários de cada elemento de estrutura gerado.");
            legenda.Adicionar(
                "LSF-PARAFUSOS | Parede <Id> | Painel <n> | <ligação>",
                "Texto do campo Comentários dos parafusos criados no 3D (um elemento por ligação, parede e painel).");

            foreach (string tipo in estrutura.Select(e => e.Tipo).Distinct().OrderBy(t => t))
            {
                string descricao;
                if (!DescricaoDosTiposDeEstrutura.TryGetValue(tipo, out descricao)) descricao = "(sem descrição)";
                legenda.Adicionar(tipo, descricao);
            }

            dados.Secoes.Add(legenda);

            var paineis = new SecaoQuantitativo
            {
                Aba = "Paineis",
                Titulo = "PAREDES E PAINEIS",
                Colunas = new[] { "Parede", "Painel", "Elementos de estrutura", "Pecas de placa" }
            };

            List<Tuple<int, int>> chaves = estrutura.Select(e => Tuple.Create(e.ParedeId, e.Painel))
                .Concat(placas.Select(p => Tuple.Create(p.ParedeId, p.Painel)))
                .Distinct()
                .OrderBy(k => k.Item1).ThenBy(k => k.Item2)
                .ToList();

            foreach (Tuple<int, int> chave in chaves)
            {
                paineis.Adicionar(
                    chave.Item1,
                    chave.Item2,
                    estrutura.Count(e => e.ParedeId == chave.Item1 && e.Painel == chave.Item2),
                    placas.Count(p => p.ParedeId == chave.Item1 && p.Painel == chave.Item2));
            }

            dados.Secoes.Add(paineis);
        }

        // ------------------------------------------------------------------------------------
        // Leitura das placas
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Medida da peca e recortes, lidos do campo Comentarios da placa (o texto que o gerador de
        /// placas grava): "... | <codigo> | L x A mm | placa ... | <codigo>-R1 L x A mm".
        /// </summary>
        private static DetalheDaPeca LerDetalhe(PlacaExistente placa)
        {
            var detalhe = new DetalheDaPeca();

            string comentario = placa.Elemento != null
                ? placa.Elemento.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()
                : null;
            if (string.IsNullOrEmpty(comentario)) return detalhe;

            string[] seg = comentario.Split(new[] { " | " }, StringSplitOptions.None);

            if (seg.Length > 6 && PadraoMedida.IsMatch(seg[6].Trim()))
            {
                detalhe.Medida = seg[6].Trim();
            }

            foreach (string trecho in seg)
            {
                Match m = PadraoRecorte.Match(trecho.Trim());
                if (!m.Success) continue;

                detalhe.Recortes.Add(new RecorteLido
                {
                    Codigo = m.Groups["cod"].Value,
                    LarguraMm = LerNumero(m.Groups["l"].Value),
                    AlturaMm = LerNumero(m.Groups["a"].Value)
                });
            }

            return detalhe;
        }

        private static double LerNumero(string texto)
        {
            double valor;
            return double.TryParse(
                texto.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor) ? valor : 0.0;
        }

        /// <summary>"1234-P2-F1-03-R1" vira "R1".</summary>
        private static string SufixoDoRecorte(string codigoDoRecorte)
        {
            int indice = codigoDoRecorte.LastIndexOf("-R", StringComparison.Ordinal);
            return indice >= 0 ? codigoDoRecorte.Substring(indice + 1) : codigoDoRecorte;
        }

        private static string N(double valor, int casas = 2)
        {
            return valor.ToString("F" + casas, Pt);
        }

        /// <summary>Uma linha do CSV (separador ponto e virgula), com aspas onde preciso.</summary>
        private static string Linha(params object[] campos)
        {
            return string.Join(";", campos.Select(c =>
            {
                string texto = Convert.ToString(c, Pt) ?? "";
                return texto.IndexOfAny(new[] { ';', '"', '\n' }) >= 0
                    ? "\"" + texto.Replace("\"", "\"\"") + "\""
                    : texto;
            }));
        }
    }
}