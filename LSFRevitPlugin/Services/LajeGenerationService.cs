using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Orquestra a geracao da laje: orientacao das vigas -> layout -> camadas -> geometria,
    /// criando os DirectShapes numa unica TransactionGroup (um so Ctrl+Z). Na mesma linha de
    /// StructureGenerationService.
    ///
    /// Tudo roda dentro de UMA transacao, precedida da remocao dos elementos anteriores da MESMA
    /// laje (identidade por LajeId), de modo que regerar a mesma laje nao duplica nada. Os
    /// problemas entram como avisos/erros no ResultadoGeracaoLaje, nao como excecoes soltas.
    ///
    /// Escopo da T11: vigas de perfil simples (alma vertical) e guias de borda. As vigas de borda
    /// ficam com o EIXO sobre o contorno (LayoutVigas); o recuo de meia aba e aplicado aqui, na
    /// geometria, deslocando o eixo da viga de borda AbaMm/2 para dentro.
    /// </summary>
    public sealed class LajeGenerationService
    {
        private const string NomeViga = "LSF - Viga de laje";
        private const string NomeGuiaBorda = "LSF - Guia de borda (laje)";
        private const string NomeTravamento = "LSF - Travamento (trelica)";
        private const string NomeFita = "LSF - Fita tensionada";
        private const string NomeCamada = "LSF - Camada de laje";

        public ResultadoGeracaoLaje Gerar(Document doc, ParametrosLaje parametros, int lajeId)
        {
            return Gerar(doc, parametros, lajeId, TipoOrigemContorno.BordasSelecionadas, -1);
        }

        /// <summary>
        /// Gera (ou regenera) a laje. <paramref name="origem"/> e <paramref name="origemElementoId"/>
        /// descrevem de onde veio o contorno e ficam guardados no registro da laje, para permitir
        /// listar, editar e regenerar depois.
        ///
        /// Regeneracao segura: o layout e as camadas sao calculados ANTES de abrir a transacao;
        /// dentro dela, os elementos anteriores da MESMA laje sao apagados e os novos criados.
        /// Se nada for criado, a transacao e desfeita, restaurando a laje anterior (nao fica sem
        /// laje). Qualquer excecao tambem desfaz o grupo inteiro.
        /// </summary>
        public ResultadoGeracaoLaje Gerar(
            Document doc,
            ParametrosLaje parametros,
            int lajeId,
            TipoOrigemContorno origem,
            int origemElementoId)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var resultado = new ResultadoGeracaoLaje { LajeId = lajeId };

            if (parametros == null)
            {
                resultado.Erros.Add("Parametros da laje nao informados.");
                return resultado;
            }

            List<string> errosParametros = parametros.Validar();
            if (errosParametros.Count > 0)
            {
                resultado.Erros.AddRange(errosParametros);
                return resultado;
            }

            List<string> errosDirecao;
            double? direcao = parametros.DirecaoEfetivaGraus(out errosDirecao);
            if (errosDirecao.Count > 0)
            {
                resultado.Erros.AddRange(errosDirecao);
                return resultado;
            }

            OrientacaoVigas orientacao;
            ResultadoLayoutVigas layout;
            CamadasLaje camadas;
            try
            {
                orientacao = OrientacaoVigas.Calcular(parametros.Contorno, direcao);
                layout = LayoutVigas.Calcular(
                    parametros.Contorno,
                    orientacao,
                    parametros.EspacamentoMm,
                    posicoesApoioMm: PosicoesApoio(doc, parametros, orientacao, resultado.Avisos),
                    toleranciaAlinhamentoMm: CriteriosLaje.CriarPadrao().ToleranciaAlinhamentoMm.Valor,
                    larguraVigaMm: ComposicaoViga.LarguraTransversalMm(
                        parametros.TipoViga, parametros.Perfil, parametros.MontagemTrelica,
                        parametros.ComposicaoCaixao));
                camadas = CamadasLaje.Calcular(parametros, CatalogoLaje.CriarPadrao());
            }
            catch (Exception ex)
            {
                resultado.Erros.Add("Layout das vigas: " + ex.Message);
                return resultado;
            }

            resultado.Avisos.AddRange(layout.Avisos);
            if (!layout.EhValido)
            {
                resultado.Erros.AddRange(layout.Erros);
                return resultado;
            }

            double zCentroVigaMm = (camadas.TopoVigaMm + camadas.BaseVigaMm) / 2.0;
            bool sucesso = false;

            using (var grupo = new TransactionGroup(doc, "LSF - Gerar/regenerar laje"))
            {
                grupo.Start();

                try
                {
                    using (var transacao = new Transaction(doc, "LSF - Gerar laje"))
                    {
                        transacao.Start();

                        resultado.RemovidosAnteriores = RemoverLajeAnterior(doc, lajeId);

                        ElementId materialViga = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.VigaLaje);
                        ElementId materialGuiaBorda = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.GuiaBordaLaje);
                        ElementId materialParafuso = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.ParafusoLaje);
                        ElementId materialTelha = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.TelhaLaje);
                        ElementId materialArgamassa = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.ArgamassaLaje);
                        ElementId materialGusset = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.GussetLaje);
                        ElementId materialTravamento = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.TravamentoLaje);
                        ElementId materialFita = LsfMateriais.ObterOuCriar(
                            doc, parametros.Perfil.Sistema, PapelElemento.FitaLaje);

                        CriarVigas(
                            doc, parametros, orientacao, layout, zCentroVigaMm, materialViga, materialGusset, lajeId, resultado);
                        CriarBordas(
                            doc, parametros, orientacao, layout, zCentroVigaMm,
                            materialGuiaBorda, materialViga, materialGusset, materialTravamento, lajeId, resultado);
                        CriarFitasTensionadas(
                            doc, parametros, orientacao, layout, camadas, materialFita, lajeId, resultado);
                        CriarCamadas(doc, parametros, orientacao, camadas, lajeId, resultado);
                        CriarTelhaEAArgamassa(
                            doc, parametros, orientacao, camadas, materialTelha, materialArgamassa, lajeId, resultado);
                        CriarFixadores(
                            doc, parametros, orientacao, layout, camadas, zCentroVigaMm,
                            materialParafuso, lajeId, resultado);

                        if (resultado.TotalCriados == 0)
                        {
                            resultado.Erros.Add(
                                "Nenhum elemento foi gerado; a laje anterior (se havia) foi restaurada.");
                            transacao.RollBack();
                        }
                        else
                        {
                            RegistroLajesService.Salvar(doc, new RegistroLaje
                            {
                                LajeId = lajeId,
                                OrigemTipo = origem,
                                OrigemElementoId = origemElementoId,
                                Parametros = parametros
                            });

                            transacao.Commit();
                            sucesso = true;
                        }
                    }

                    if (sucesso) grupo.Assimilate();
                    else grupo.RollBack();
                }
                catch (Exception ex)
                {
                    try { grupo.RollBack(); }
                    catch { /* melhor esforco: se o rollback falhar, propaga a falha original */ }

                    resultado.Erros.Add("Falha ao gerar a laje: " + ex.Message);
                    return resultado;
                }
            }

            if (sucesso) MedirPrimeiraViga(doc, lajeId, resultado);
            return resultado;
        }

        private static void CriarVigas(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            ResultadoLayoutVigas layout,
            double zCentroVigaMm,
            ElementId materialViga,
            ElementId materialGusset,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            double meiaLargura = ComposicaoViga.LarguraTransversalMm(
                parametros.TipoViga, parametros.Perfil, parametros.MontagemTrelica,
                parametros.ComposicaoCaixao) / 2.0;
            if (meiaLargura <= 0.0) meiaLargura = parametros.Perfil.AbaMm / 2.0;
            int numero = 0;

            foreach (VigaLaje viga in layout.Vigas)
            {
                double vMm = viga.PosicaoTransversalMm;

                bool naBordaMin = Math.Abs(vMm - orientacao.VMinMm) <= LayoutVigas.ToleranciaMm;
                bool naBordaMax = Math.Abs(vMm - orientacao.VMaxMm) <= LayoutVigas.ToleranciaMm;

                double vEfetivoMm = vMm;
                if (naBordaMin) vEfetivoMm = vMm + meiaLargura;
                else if (naBordaMax) vEfetivoMm = vMm - meiaLargura;

                foreach (SegmentoViga segmento in viga.Segmentos)
                {
                    if (segmento.ComprimentoMm <= LayoutVigas.ToleranciaMm) continue;

                    XYZ inicio = PontoMundo(orientacao, segmento.InicioUMm, vEfetivoMm, zCentroVigaMm);
                    XYZ fim = PontoMundo(orientacao, segmento.FimUMm, vEfetivoMm, zCentroVigaMm);

                    List<Solid> solidos;
                    if (parametros.TipoViga == TipoVigaLaje.Trelica)
                    {
                        int paineis = parametros.NumeroPaineisTrelica
                            ?? ComposicaoTrelica.NumeroPaineis(
                                segmento.ComprimentoMm, parametros.PainelTrelicaAlvoMm);

                        var normalPlano = new XYZ(orientacao.VX, orientacao.VY, 0.0);
                        solidos = TrelicaBuilder.CriarSolidosTrelica(
                            parametros.Perfil, parametros.Trelica, segmento.ComprimentoMm,
                            parametros.AlturaTrelicaMm, paineis, inicio, fim, normalPlano, materialViga,
                            parametros.MontagemTrelica);

                        if (parametros.GussetPlacas)
                        {
                            List<MembroTrelica> membrosTrelica = ComposicaoTrelica.Membros(
                                parametros.Trelica, segmento.ComprimentoMm, parametros.AlturaTrelicaMm, paineis);
                            var posGussets = GussetLaje.Calcular(membrosTrelica, segmento.ComprimentoMm, parametros.AlturaTrelicaMm);
                            List<Solid> sGus = GussetBuilder.CriarSolidosGussets(posGussets, inicio, new XYZ(orientacao.UX, orientacao.UY, 0.0), normalPlano, materialGusset);
                            foreach (Solid sg in sGus)
                            {
                                solidos.Add(sg);
                            }
                            resultado.ChapasGussetCriadas += sGus.Count;
                        }
                    }
                    else
                    {
                        solidos = LajeVigaBuilder.CriarSolidosViga(
                            parametros.Perfil, parametros.TipoViga, inicio, fim, materialViga,
                            parametros.ComposicaoCaixao);
                    }

                    if (solidos.Count == 0)
                    {
                        resultado.Avisos.Add("Viga nao gerada: segmento degenerado.");
                        continue;
                    }

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solidos, NomeViga);
                    if (ds == null)
                    {
                        resultado.Avisos.Add("Viga nao gerada: falha ao criar o DirectShape.");
                        continue;
                    }

                    GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Viga, numero);
                    numero++;
                    resultado.VigasCriadas++;
                }
            }
        }

        /// <summary>
        /// Cria o membro de borda em cada aresta perpendicular as vigas. Para vigas convencionais
        /// e a guia "U" de borda; para vigas trelicadas e uma trelica transversal de travamento,
        /// que recebe as pontas das trelicas nos DOIS apoios (ver TravamentoLaje). A guia "U" so e
        /// usada quando a laje nao for de vigas trelicadas.
        /// </summary>
        private static void CriarBordas(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            ResultadoLayoutVigas layout,
            double zCentroVigaMm,
            ElementId materialGuiaBorda,
            ElementId materialViga,
            ElementId materialGusset,
            ElementId materialTravamento,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            TipoMembroBorda tipoMembro = TravamentoLaje.MembroDeBorda(parametros.TipoViga);
            double centroUMm = (orientacao.UMinMm + orientacao.UMaxMm) / 2.0;
            int numero = 0;

            foreach (GuiaBordaLaje guia in layout.GuiasBorda)
            {
                if (guia.ComprimentoMm <= LayoutVigas.ToleranciaMm) continue;

                XYZ inicio = PontoMundo(orientacao, guia.UMm, guia.VInicioMm, zCentroVigaMm);
                XYZ fim = PontoMundo(orientacao, guia.UMm, guia.VFimMm, zCentroVigaMm);

                if (tipoMembro == TipoMembroBorda.TrelicaTravamento)
                {
                    CriarTravamentoTrelica(
                        doc, parametros, orientacao, guia, inicio, fim, zCentroVigaMm,
                        materialTravamento, materialGusset, lajeId, numero, resultado);
                }
                else
                {
                    double sinal = centroUMm >= guia.UMm ? 1.0 : -1.0;
                    XYZ paraDentro = new XYZ(orientacao.UX * sinal, orientacao.UY * sinal, 0.0);

                    Solid solido = LajeVigaBuilder.CriarSolidoGuiaBorda(
                        parametros.Perfil, inicio, fim, paraDentro, materialGuiaBorda);
                    if (solido == null)
                    {
                        resultado.Avisos.Add("Guia de borda nao gerada: segmento degenerado.");
                        continue;
                    }

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solido, NomeGuiaBorda);
                    if (ds == null)
                    {
                        resultado.Avisos.Add("Guia de borda nao gerada: falha ao criar o DirectShape.");
                        continue;
                    }

                    GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.GuiaBorda, numero);
                    resultado.GuiasBordaCriadas++;
                }

                numero++;
            }
        }

        private static void CriarTravamentoTrelica(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            GuiaBordaLaje guia,
            XYZ inicio,
            XYZ fim,
            double zCentroVigaMm,
            ElementId materialTravamento,
            ElementId materialGusset,
            int lajeId,
            int numero,
            ResultadoGeracaoLaje resultado)
        {
            int paineis = parametros.NumeroPaineisTrelica
                ?? ComposicaoTrelica.NumeroPaineis(guia.ComprimentoMm, parametros.PainelTrelicaAlvoMm);

            var normalPlano = new XYZ(orientacao.UX, orientacao.UY, 0.0);
            List<Solid> solidos = TrelicaBuilder.CriarSolidosTrelica(
                parametros.Perfil, parametros.Trelica, guia.ComprimentoMm,
                parametros.AlturaTrelicaMm, paineis, inicio, fim, normalPlano, materialTravamento,
                parametros.MontagemTrelica);

            if (solidos.Count == 0)
            {
                resultado.Avisos.Add("Travamento da trelica nao gerado: segmento degenerado.");
                return;
            }

            if (parametros.GussetPlacas)
            {
                List<MembroTrelica> membros = ComposicaoTrelica.Membros(
                    parametros.Trelica, guia.ComprimentoMm, parametros.AlturaTrelicaMm, paineis);
                var posGussets = GussetLaje.Calcular(
                    membros, guia.ComprimentoMm, parametros.AlturaTrelicaMm);
                List<Solid> gussets = GussetBuilder.CriarSolidosGussets(
                    posGussets, inicio, new XYZ(orientacao.VX, orientacao.VY, 0.0), normalPlano, materialGusset);
                foreach (Solid g in gussets) solidos.Add(g);
                resultado.ChapasGussetCriadas += gussets.Count;
            }

            DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solidos, NomeTravamento);
            if (ds == null)
            {
                resultado.Avisos.Add("Travamento da trelica nao gerado: falha ao criar o DirectShape.");
                return;
            }

            GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Travamento, numero);
            resultado.TravamentosCriados++;
        }

        /// <summary>
        /// Cria as fitas tensionadas que travam as trelicas entre si, quando a opcao esta ligada.
        /// As posicoes sao GEOMETRICAS DE PARTIDA (ver FitasTensionadasLaje), nao normativas.
        /// </summary>
        private static void CriarFitasTensionadas(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            ResultadoLayoutVigas layout,
            CamadasLaje camadas,
            ElementId materialFita,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            if (!parametros.FitasTensionadas) return;
            if (parametros.TipoViga != TipoVigaLaje.Trelica) return;

            List<FitaTensionada> fitas = FitasTensionadasLaje.Calcular(
                orientacao, layout, parametros.EspacamentoFitasTensionadasMm);
            if (fitas.Count == 0) return;

            double zTopoBanzoMm = (camadas.TopoVigaMm + camadas.BaseVigaMm) / 2.0
                + parametros.AlturaTrelicaMm / 2.0;
            int numero = 0;

            foreach (FitaTensionada fita in fitas)
            {
                Solid solido = FitaBuilder.CriarSolidoFita(fita, orientacao, zTopoBanzoMm, materialFita);
                if (solido == null) continue;

                DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solido, NomeFita);
                if (ds == null) continue;

                GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Fita, numero);
                numero++;
                resultado.FitasCriadas++;
            }
        }

        /// <summary>
        /// Cria as placas planas de cobertura presentes nas camadas (piso seco, OSB, cimenticia).
        /// Quando a composicao tem placas e o parcelamento esta ligado, cada camada e dividida em
        /// chapas comerciais com juntas (OSB/cimenticia); caso contrario sai uma placa unica.
        /// Telha e argamassa sao geradas por outros metodos. Ver docs.
        /// </summary>
        private static void CriarCamadas(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            CamadasLaje camadas,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            int numero = 0;
            int camadaPlaca = 0;

            foreach (CamadaLaje camada in camadas.Camadas)
            {
                if (!CamadaLaje.EhPlaca(camada.Nome)) continue;

                bool osb = PaginacaoPlacas.EhCamadaOSB(camada.Nome);
                PapelElementoLaje papel = osb ? PapelElementoLaje.CamadaOsb : PapelElementoLaje.CamadaCimenticia;
                PapelElemento papelMaterial = osb ? PapelElemento.OsbLaje : PapelElemento.CimenticiaLaje;
                ElementId material = LsfMateriais.ObterOuCriar(doc, parametros.Perfil.Sistema, papelMaterial);

                // Regra fixa: toda composicao de placas (OSB/cimenticia) e sempre paginada em
                // chapas comerciais com junta fixa. "Nenhuma" usa a placa unica do catalogo.
                bool paginar = parametros.ComposicaoPlacas != ComposicaoTipo.Nenhuma;

                if (paginar)
                {
                    double largura;
                    double comprimento;
                    PaginacaoPlacas.DimensoesOrientadas(osb, out largura, out comprimento);

                    double offsetCamada = (camadaPlaca % 2 == 0) ? 0.0 : comprimento / 2.0;
                    double offsetLinha = largura / 2.0;

                    ResultadoPaginacao paginacao = PaginacaoPlacas.Calcular(
                        parametros.Contorno, orientacao, largura, comprimento,
                        camada.BaseMm, camada.EspessuraMm, PaginacaoPlacas.JuntaPadraoMm,
                        offsetCamada, offsetLinha, camadaPlaca);
                    resultado.Avisos.AddRange(paginacao.Avisos);

                    foreach (PlacaPaginada placa in paginacao.Placas)
                    {
                        Solid solido = LajeVigaBuilder.CriarSolidoPlaca(
                            placa.ContornoMundo, placa.BaseMm, placa.EspessuraMm, material);
                        if (solido == null) continue;

                        DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solido, NomeCamada);
                        if (ds == null) continue;

                        GravarIdentidade(ds, lajeId, parametros.Tipo, papel, numero);
                        numero++;
                        resultado.CamadasCriadas++;
                    }
                }
                else
                {
                    Solid solido = LajeVigaBuilder.CriarSolidoPlaca(
                        parametros.Contorno, camada.BaseMm, camada.EspessuraMm, material);
                    if (solido == null)
                    {
                        resultado.Avisos.Add("Camada nao gerada: contorno degenerado.");
                        camadaPlaca++;
                        continue;
                    }

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solido, NomeCamada);
                    if (ds == null)
                    {
                        resultado.Avisos.Add("Camada nao gerada: falha ao criar o DirectShape.");
                        camadaPlaca++;
                        continue;
                    }

                    GravarIdentidade(ds, lajeId, parametros.Tipo, papel, numero);
                    numero++;
                    resultado.CamadasCriadas++;
                }

                camadaPlaca++;
            }
        }

        /// <summary>
        /// Cria os fixadores (parafusos) no nivel de detalhe escolhido. Estruturais ficam nas
        /// pontas das vigas sobre as guias; os de placa, ao longo das vigas na cota da placa de
        /// cobertura. Sem placa (laje umida), so saem os estruturais. Oculto nao cria nada.
        /// </summary>
        private static void CriarFixadores(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            ResultadoLayoutVigas layout,
            CamadasLaje camadas,
            double zCentroVigaMm,
            ElementId materialParafuso,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            if (parametros.NivelFixadores == NivelDetalheFixadores.Oculto) return;

            double zPlacaMm = zCentroVigaMm;
            CamadaLaje placa = CamadaDePlacaMaisAlta(camadas);
            if (placa != null) zPlacaMm = (placa.TopoMm + placa.BaseMm) / 2.0;

            ResultadoFixadores fixadores = FixadoresLaje.Calcular(
                layout,
                parametros.NivelFixadores,
                zCentroVigaMm,
                zPlacaMm,
                parametros.EspacamentoFixadoresPlacaMm);
            resultado.Avisos.AddRange(fixadores.Avisos);
            if (fixadores.Oculto) return;

            int numero = 0;
            int foraDoContorno = 0;
            numero += CriarFixadoresDe(fixadores.Estruturais, "LSF - Parafuso estrutural");
            numero += CriarFixadoresDe(fixadores.Placa, "LSF - Parafuso de placa");
            resultado.ParafusosCriados += numero;

            if (foraDoContorno > 0)
            {
                resultado.Avisos.Add(string.Format(
                    "Fixadores ignorados por ficarem fora do contorno: {0}.", foraDoContorno));
            }

            int CriarFixadoresDe(List<PosicaoFixador> posicoes, string nome)
            {
                int criados = 0;

                foreach (PosicaoFixador pos in posicoes)
                {
                    if (parametros.Contorno != null)
                    {
                        Ponto2DMm plano = orientacao.ParaMundo(pos.UMm, pos.VMm);
                        if (!RecortePoligono.Contem(parametros.Contorno.Pontos, plano))
                        {
                            foraDoContorno++;
                            continue;
                        }
                    }

                    XYZ centro = PontoMundo(orientacao, pos.UMm, pos.VMm, pos.ZMm);
                    XYZ direcao = pos.Direcao == DirecaoFixador.AoLongoDaViga
                        ? new XYZ(orientacao.UX, orientacao.UY, 0.0)
                        : XYZ.BasisZ;
                    List<Solid> solidos = FixadorBuilder.CriarSolidosParafuso(
                        centro, direcao, parametros.NivelFixadores, materialParafuso);
                    if (solidos.Count == 0) continue;

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solidos, nome);
                    if (ds == null) continue;

                    GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Parafuso, numero++);
                    criados++;
                }

                return criados;
            }
        }

        private static CamadaLaje CamadaPorNome(CamadasLaje camadas, string nome)
        {
            return camadas?.Camada(nome);
        }

        private static CamadaLaje CamadaDePlacaMaisAlta(CamadasLaje camadas)
        {
            if (camadas == null) return null;

            CamadaLaje maisAlta = null;
            foreach (CamadaLaje c in camadas.Camadas)
            {
                if (!CamadaLaje.EhPlaca(c.Nome)) continue;
                if (maisAlta == null || c.TopoMm > maisAlta.TopoMm) maisAlta = c;
            }

            return maisAlta;
        }

        /// <summary>
        /// Cria a telha trapezoidal e a argamassa da laje umida. A telha e gerada por faixas do
        /// perfil recortadas pelo contorno real da laje (TelhamentoTp40), uma chapa (DirectShape) por
        /// coluna. A argamassa inclui o enchimento das ondas (quando PreencherOndas) e a placa plana
        /// acima da onda. Nao faz nada para laje seca.
        /// </summary>
        private static void CriarTelhaEAArgamassa(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            CamadasLaje camadas,
            ElementId materialTelha,
            ElementId materialArgamassa,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            if (parametros.Tipo != TipoLaje.Umida || camadas == null) return;

            CamadaLaje camadaTelha = CamadaPorNome(camadas, CamadaLaje.NomeTelha);
            CamadaLaje camadaArgamassa = CamadaPorNome(camadas, CamadaLaje.NomeArgamassa);
            TelhaTp40 telha = CatalogoLaje.CriarPadrao().Telha;

            if (camadaTelha != null)
            {
                ResultadoFaixas faixas = TelhamentoTp40.CalcularTelha(
                    parametros.Contorno, orientacao, telha, parametros.EspessuraTelhaMm);

                resultado.Avisos.AddRange(faixas.Avisos);

                int numero = 0;
                foreach (var grupo in AgruparPorColuna(faixas.Faixas))
                {
                    List<Solid> solidos = TelhaBuilder.CriarSolidosFaixas(
                        grupo, orientacao, camadaTelha.BaseMm, materialTelha);
                    if (solidos.Count == 0) continue;

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(
                        doc, solidos, "LSF - Telha");
                    if (ds == null) continue;

                    GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Telha, numero);
                    numero++;
                    resultado.TelhasCriadas++;
                }

                if (resultado.TelhasCriadas == 0)
                {
                    resultado.Avisos.Add("Telha nao gerada: geometria degenerada.");
                }
            }

            if (camadaArgamassa != null)
            {
                if (parametros.PreencherOndas)
                {
                    CriarEnchimentoOndas(
                        doc, parametros, orientacao, camadaArgamassa, telha, materialArgamassa, lajeId, resultado);
                }

                Solid solido = LajeVigaBuilder.CriarSolidoPlaca(
                    parametros.Contorno, camadaArgamassa.BaseMm, camadaArgamassa.EspessuraMm, materialArgamassa);

                if (solido == null)
                {
                    resultado.Avisos.Add("Argamassa nao gerada: contorno degenerado.");
                }
                else
                {
                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(
                        doc, solido, "LSF - Argamassa");
                    if (ds != null)
                    {
                        GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Argamassa, 0);
                        resultado.ArgamassasCriadas++;
                    }
                }
            }
        }

        /// <summary>Cria o enchimento das ondas, uma DirectShape por coluna da telha.</summary>
        private static void CriarEnchimentoOndas(
            Document doc,
            ParametrosLaje parametros,
            OrientacaoVigas orientacao,
            CamadaLaje camadaArgamassa,
            TelhaTp40 telha,
            ElementId material,
            int lajeId,
            ResultadoGeracaoLaje resultado)
        {
            ResultadoFaixas enchimento = TelhamentoTp40.CalcularEnchimento(
                parametros.Contorno, orientacao, telha);

            resultado.Avisos.AddRange(enchimento.Avisos);

            int numero = 0;
            foreach (var grupo in AgruparPorColuna(enchimento.Faixas))
            {
                List<Solid> solidos = TelhaBuilder.CriarSolidosFaixas(
                    grupo, orientacao, camadaArgamassa.BaseMm, material);
                if (solidos.Count == 0) continue;

                DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(
                    doc, solidos, "LSF - Enchimento ondas");
                if (ds == null) continue;

                GravarIdentidade(ds, lajeId, parametros.Tipo, PapelElementoLaje.Enchimento, numero);
                numero++;
                resultado.ArgamassasCriadas++;
            }
        }

        private static IEnumerable<List<FaixaPerfil>> AgruparPorColuna(IEnumerable<FaixaPerfil> faixas)
        {
            var ordem = new List<int>();
            var mapa = new Dictionary<int, List<FaixaPerfil>>();

            foreach (FaixaPerfil f in faixas)
            {
                if (!mapa.TryGetValue(f.Coluna, out List<FaixaPerfil> lista))
                {
                    lista = new List<FaixaPerfil>();
                    mapa[f.Coluna] = lista;
                    ordem.Add(f.Coluna);
                }

                lista.Add(f);
            }

            foreach (int coluna in ordem) yield return mapa[coluna];
        }

        /// <summary>
        /// Posicoes v (mm) dos montantes de parede para alinhar as vigas internas; nulo quando nao
        /// ha apoios (as vigas permanecem na grade). Os avisos entram no resultado do chamador.
        /// </summary>
        private static IReadOnlyList<double> PosicoesApoio(
            Document doc, ParametrosLaje parametros, OrientacaoVigas orientacao, List<string> avisos)
        {
            List<double> apoios = ApoiosLajeService.PosicoesApoioMm(
                doc, parametros.Contorno, orientacao, out List<string> avisoApoios);
            avisos.AddRange(avisoApoios);
            return apoios.Count > 0 ? apoios : null;
        }

        private static XYZ PontoMundo(OrientacaoVigas orientacao, double uMm, double vMm, double zMm)
        {
            Ponto2DMm ponto = orientacao.ParaMundo(uMm, vMm);
            return new XYZ(
                RevitVersionAdapter.MilimetrosParaInterno(ponto.XMm),
                RevitVersionAdapter.MilimetrosParaInterno(ponto.YMm),
                RevitVersionAdapter.MilimetrosParaInterno(zMm));
        }

        private static void GravarIdentidade(
            DirectShape ds, int lajeId, TipoLaje tipo, PapelElementoLaje papel, int numero)
        {
            PertencenciaLajeService.Gravar(ds, lajeId, tipo.ToString(), papel.ToString(), numero);

            Parameter comentarios = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comentarios != null && !comentarios.IsReadOnly)
            {
                comentarios.Set(IdentidadeLajeCodec.MontarTexto(lajeId, tipo, papel, numero));
            }
        }

        /// <summary>Apaga os elementos da laje anterior com o mesmo LajeId. Devolve quantos removeu.</summary>
        private static int RemoverLajeAnterior(Document doc, int lajeId)
        {
            var remover = new List<Element>();

            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                int? id = ObterLajeId(ds);
                if (id.HasValue && id.Value == lajeId) remover.Add(ds);
            }

            foreach (Element elemento in remover) doc.Delete(elemento.Id);
            return remover.Count;
        }

        /// <summary>LajeId do elemento: Extensible Storage primeiro, texto de Comentarios como fallback.</summary>
        private static int? ObterLajeId(DirectShape ds)
        {
            IdentidadeLaje identidade;
            if (PertencenciaLajeService.TentarLer(ds, out identidade)) return identidade.LajeId;

            string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
            if (IdentidadeLajeCodec.TentarExtrair(comentario, out identidade)) return identidade.LajeId;

            return null;
        }

        /// <summary>Mede uma viga gerada com o LeitorDeEstrutura, para o teste manual (comprimento e alma).</summary>
        private static void MedirPrimeiraViga(Document doc, int lajeId, ResultadoGeracaoLaje resultado)
        {
            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                IdentidadeLaje identidade;
                if (!PertencenciaLajeService.TentarLer(ds, out identidade)) continue;
                if (identidade.LajeId != lajeId) continue;
                if (identidade.Papel != PapelElementoLaje.Viga) continue;

                MedidaElemento medida = LeitorDeEstrutura.Medir(ds, null);
                if (medida == null) continue;

                resultado.ComprimentoVigaMedidoMm = medida.ComprimentoMm;
                resultado.AlturaAlmaMedidaMm = medida.Z1Mm - medida.Z0Mm;
                return;
            }
        }
    }
}
