# CURRENT STATE — LSF PLUGIN REVIT

## 0. MISSÃO ATUAL — LAJE: CORREÇÕES 1–4 + LOTE C/D/E (travamento, fitas, fixadores com direção, alinhamento a montantes) (2026-10-18)

Missão autônoma com 12 requisitos (Lotes A–E). Lote A/B e as 4 correções geométricas dirigidas já estavam feitos; **esta sessão** conclui o Lote C (travamento das trelicas, fitas tensionadas, fixadores com direção) e o Lote D (alinhamento de vigas internas a montantes), com testes de domínio e docs. Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, **nenhum commit**. Build e testes executados no Windows via `powershell.exe`.

- **DECISÕES DO USUÁRIO (autoritativas)**: (C) implementar a parte **geométrica de partida** e **documentar** o que falta — travamento/fitas/fixadores são PROVISÓRIOS/NÃO NORMATIVOS, sem inventar valores; (D) **ler paredes do Revit sob a laje** via novo provider.
- **IMPLEMENTADO**:
  - **Membro de borda (C/1.7)**: `Domain\Laje\TravamentoLaje.cs` — `TipoMembroBorda {GuiaBorda, TrelicaTravamento}`, `MembroDeBorda(tipoViga)` (Trelica → trelica de travamento; demais → guia "U"), `Apoios(orientacao)`. `LajeGenerationService.CriarBordas` ramifica; `CriarTravamentoTrelica` reusa `TrelicaBuilder`; `PapelElementoLaje.Travamento` + `NomeTravamento = "LSF - Travamento (trelica)"`.
  - **Fitas tensionadas (C/1.7, opt-in, PROVISÓRIO)**: `Domain\Laje\FitasTensionadasLaje.cs` (linhas em passo alvo `PassoPadraoMm=1500` estritamente dentro do vão, exige ≥2 vigas), `Geometry\FitaBuilder.cs` (30×1 mm), `ParametrosLaje.FitasTensionadas`/`EspacamentoFitasTensionadasMm`, persistência `fitasTensionadas=`/`espFitas=`, `PapelElementoLaje.Fita` + `PapelElemento.FitaLaje`, `ResultadoGeracaoLaje.FitasCriadas`. UI: `CheckFitasTensionadas` + `PanelFitas`/`TextEspacamentoFitas` (visíveis só com viga trelicada).
  - **Fixadores com direção (C/1.8)**: `DirecaoFixador {Vertical, AoLongoDaViga}`; estrutural ao longo da viga, placa vertical; `FixadorBuilder` extrui a caixa ao longo da direção (tamanhos ainda PROVISÓRIOS).
  - **Alinhamento a montantes (D/1.9)**: `Services\ApoiosLajeService.cs` lê paredes LSF sob/ao redor do contorno (reuso de `LeituraEstruturaParedeService`), projeta posições de montante para o eixo v; `LayoutVigas.Calcular` ganhou `posicoesApoioMm`/`toleranciaAlinhamentoMm` (`CriteriosLaje.ToleranciaAlinhamentoMm = 50mm`, PROVISÓRIO); viga de borda NÃO se move; sem montante correspondente → **aviso** e viga permanece na grade (nunca desloca em silêncio). Propagado por `PreviaLaje.Calcular` e `LajeConfigWindow`; `Core\LajeCommand.cs`/`EditarLajeCommand.cs` injetam o provider (fecham sobre `doc`+contorno).
- **TESTADO** (Windows, `.\rodar-testes.ps1`): **414/414 aprovados**, 0 falhas, 0 ignorados. Build `dotnet build LSFRevitPlugin.sln` → **0 avisos / 0 erros**. Novos: `LajeTravamentoTests` (membro de borda, apoios, fitas, validação, persistência); `LajeFixadoresTests` (+direção estrutural/placa); `LajePreviaTests` (+provider alinha/avisa); `LajeLayoutVigasTests` (ajustados p/ o novo aviso de viga interna sem montante); `LajeIdentidadeTests` (round-trip `Travamento`/`Fita`).
- **VALIDADO** pelos testes: regra de membro de borda, geometria das fitas, direção dos fixadores, alinhamento/tolerância/aviso e persistência. `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (Lote C/D: travamento, fitas, fixadores, alinhamento — ver `docs\VALIDACAO_MANUAL_LAJE_P1-P5.md` seções 7–10); **dimensionamento estrutural** das fitas/travamento e engenharia dos valores provisórios (fixadores, passo de fitas, tol. 50mm); `docs\LAJE.md` (rev. 2) **ausente**; divergências do TP40 (Isoeste 2018 vs Kingspan util 980/total 1032/0,65; Newtelhas altura 38) e contagem de ondas do perfil 980 → documentadas como provisórias.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 MISSÃO — LAJE LOTE A+B (A1-A5 + B1-B4: regras fixas, UI, perfis C trelica, montagem, caixão, gusset) (2026-10-16)

Nova missão autônoma consolidando testes reais do Revit 2021 em 12 requisitos, organizados em Lotes A–E. Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, **nenhum commit**. Lote A e Lote B completos; E2E no Revit 2021 segue PENDENTE (Revit aberto → usar `bin_ci` redirect).

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Lote A (A1–A5)**: paginação obrigatória (removidas propriedades `PaginarPlacas`/`JuntaPlacasMm` de `ParametrosLaje` e chaves `paginar`/`juntaPlacas` de `ConfiguracaoLajeTexto` — versão mantida 1, backward compat); orientação OSB perpendicular / cimentícia paralela via `PaginacaoPlacas.DimensoesOrientadas` + `EhCamadaOSB`; Drywall removido da UI de laje (`LajeConfigWindow`); janela redimensionável (`ResizeMode="CanResizeWithGrip"`, `DockPanel` + `ScrollViewer`); esquema vetorial da trelica no `CanvasTrelica` via `AtualizarEsquemaTrelica()`.
  - **Lote B1**: `Geometry/TrelicaBuilder.cs` reescrito — diagonais e montantes da trelica agora usam a MESMA seção "C" com aba enrijecida do perfil escolhido (`PerfilSpec.AbaMm`, `LarguraAlmaMm`, `EspessuraChapaMm`, `LabioMm`) via `StudSolidBuilder.CriarSolidoMontanteHorizontal`, em vez de caixa genérica 40mm. Banzos mantêm alma vertical (`XYZ.BasisZ`); web members usam normal horizontal do plano da trelica.
  - **Lote B2**: `Domain/Laje/ComposicaoViga.cs` ganhou enums `MontagemTrelica {Encaixada, BackToBack}` e `ComposicaoCaixaoTipo {FrenteAFrente, Encaixados}` + `SubPerfisMontagem()` + sobrecarga `LarguraTransversalMm(tipo, perfil, montagem)`. `ParametrosLaje.MontagemTrelica`/`ComposicaoCaixao`. Serviço/`LayoutVigas` usam largura considerando montagem; UI `PanelTrelica` ganhou `ComboMontagemTrelica`; `ConfiguracaoLajeTexto` serializa `montagemTrelica=`/`composicaoCaixao=` (backward compat).
  - **Lote B3**: Caixão com duas configurações (FrenteAFrente: almas coaxiais aberturas opostas; Encaixados: almas separadas por AbaMm, abas do mesmo lado) via `ComposicaoViga.SubPerfis(tipo, perfil, caixao)`; `LajeVigaBuilder.CriarSolidosViga` ganhou sobrecarga com caixão; UI `PanelCaixao` + `ComboComposicaoCaixao`.
  - **Lote B4**: chapas gusset opcionais nas juntas da trelica. `Domain/Laje/GussetLaje.cs` (novo, puro: `PosicaoGusset`, `GussetLaje.Calcular` — deduplica nós por tolerância 2mm) + `Geometry/GussetBuilder.cs` (novo: `CriarSolidosGussets` extrusão vertical na normal horizontal). `ParametrosLaje.GussetPlacas` (padrão `false`); `PapelElementoLaje.Gusset` + `PapelElemento.GussetLaje` (cor cinza azul) + `ResultadoGeracaoLaje.ChapasGussetCriadas` (incluída em `TotalCriados`); serviço `CriarVigas` recebe `materialGusset` e gera gussets quando `parametros.GussetPlacas`; persistência `gussetPlacas=`; UI `CheckGussetPlacas` no `PanelTrelica`.
  - **Testes**: `LajePaginacaoTests` (+`Junta_obrigatoria_e_fixa_em_3mm`, `Osb_e_mais_largo_transversalmente_e_cimenticia_no_sentido_contrario`); `LajeParametrosTests` (+`Perfil_Drywall_nao_e_aceito_para_laje`, `Catalogo_de_perfis_de_laje_nao_contem_drywall`, `Bitola_0_80_continua_rejeitada_na_laje`); `LajeConfiguracaoTextoTests` (+`Config_antiga_com_paginar_e_junta_placas_ainda_abre`); `LajePreviaTests` (fix: removido `p.PaginarPlacas`, +`Previa_com_composicao_sempre_parcela_sem_opcao`); `LajeGussetTests` (novo: `Gusset_ocorre_em_nos_com_conexao_da_trelica_Warren`, `Gusset_encerra_sem_erro_quando_sem_membros`).
- **TESTADO** (via `powershell.exe`, WSL→Windows, `.\rodar-testes.ps1`): **375/375 aprovados**, 0 falhas, 0 ignorados. Build `dotnet build LSFRevitPlugin.sln -p:OutputPath=bin_ci\` → **0 avisos / 0 erros**. Sem regressão (telha 0,65 lança; piso seco OSB 18 mm preservado; Drywall rejeitado na laje).
- **VALIDADO** pelos testes: paginação obrigatória/junta fixa 3mm, orientação OSB/cimentícia, parâmetros de laje (Drywall/0,80), persistência backward compat, prévia sem opção de paginação, gusset em nós da trelica. `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (TC-058..TC-074 — tudo do Lote A/B: paginação, orientação, redimensionamento, esquema trelica, perfis C, montagem, caixão config, gusset); Lote C (bracing/fasteners detalhados), Lote D (coordenação geométrica/cobertura), Lote E (integração/validação completa + relatório final); `docs\LAJE.md` (rev. 2) **ausente**; furos internos do contorno não representáveis (`Aviso`, não modelado). Gusset: valores puramente geométricos de partida (80×80×1,5 mm, altura no centro do nó), não dimensionados estruturalmente.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 MISSÃO ANTERIOR — LAJE P1..P5 (recorte da telha, enchimento, paginação não convexa, caixão, fixadores) (2026-10-16)

Missão autônoma de desenvolvimento do módulo de lajes com 6 prioridades (P1..P6). Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, **nenhum commit**. O bloco §0.1 (Lote 2) continua válido; o E2E no Revit 2021 segue PENDENTE.

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **P4 — Paginação de contorno não convexo**: `Domain\Laje\PaginacaoPlacas.cs` foi refatorado para delegar a `RecortePoligono`; a queda em "placa única" para contorno côncavo foi **removida**. Agora o contorno é triangulado e cada triângulo é recortado por célula; os pedaços são **costurados** pelas arestas internas comuns, admitindo mais de um pedaço por célula. Emite `Aviso` documentando a triangulação.
  - **`Domain\Laje\RecortePoligono.cs` (NOVO, puro)**: área, convexidade, sentido anti-horário, ponto-dentro (com borda), varredura vertical `IntervalosV` (convexa ou côncava), recorte Sutherland–Hodgman `Recortar`, `TriangulosConvexos`, `RecortarConvexosPorCelula`, costura de arestas `Costurar`, triangulação `Triangular` (ear clipping) e `LimparRepetidos`. Compartilhado por paginação e telhamento.
  - **P5 — Caixão**: `ComposicaoViga` ganhou a composição Caixão (2 "C" com aberturas **frente a frente** no mesmo eixo; largura transversal = Aba). `CatalogoTiposVigaLaje.Caixao.Implementado = true`; `ParametrosLaje.Validar()` passa a aceitar Caixão; UI continua oferecendo os tipos implementados. (A restrição antiga "Caixão continua não implementado" foi **cancelada por P5**.)
  - **P1 — Telha TP40 recortada pelo contorno**: `SecaoTelhaTp40` ganhou `PerfilInferior`/`EspessuraModelada` (o `Contorno` foi refatorado para reusar). **NOVO** `Domain\Laje\TelhamentoTp40.cs`: gera `FaixaPerfil` (A/B no fundo, C/D no topo; `PapelFaixa {Telha,Enchimento}`; `Coluna`/`Segmento`) varrendo o contorno em u (`RecortePoligono.IntervalosV`), colunas de largura útil. `Geometry\TelhaBuilder.cs` foi reescrito para criar sólidos a partir das faixas (`CriarSolidoDaFaixa`/`CriarSolidosFaixas`), com deduplicação de vértices (rampas de enchimento viram triângulo válido). Avisos: contorno não retangular (borda serrilhada) e comprimento acima do máximo da chapa (transpasse não modelado).
  - **P2 — Enchimento das ondas 3D**: `TelhamentoTp40.CalcularEnchimento` preenche a região entre a linha de fundo e a crista. `ParametrosLaje.PreencherOndas` (padrão `true`; persistência `preencherOndas=`; UI: `PanelPreencherOndas`/`CheckPreencherOndas`). `PapelElementoLaje.Enchimento` + `PapelElemento.EnchimentoLaje` (com cor). A espessura equivalente confere com `TelhaTp40.EnchimentoOndasM3PorM2`.
  - **P3 — Fixadores**: na geração, fixadores fora do contorno são descartados (`RecortePoligono.Contem` via conversão `orientacao.ParaMundo`) com `Aviso` da contagem. Novos testes de domínio de posição/passo/relação com as guias e do filtro de contorno.
  - **Serviço**: `LajeGenerationService.CriarTelhaEAArgamassa` cria a telha por **uma DirectShape por coluna** (`PapelElementoLaje.Telha`) e o enchimento por coluna (`PapelElementoLaje.Enchimento`), além da placa plana de argamassa acima da onda (inalterada).
- **TESTADO** (via `powershell.exe`, WSL→Windows, `dotnet test -p:OutputPath=bin_ci\` para contornar o DLL travado pelo Revit aberto): **366/366 aprovados**, 0 falhas, 0 ignorados. Novos/atualizados: `LajeRecortePoligonoTests` (novo), `LajeTelhamentoTests` (novo), `LajePaginacaoTests` (contorno não convexo agora é parcelado), `LajeFixadoresTests` (+4), `LajeRefinamentoTests`/`LajeParametrosTests` (P5 caixão). Sem regressão (telha 0,65 continua lançando; piso padrão seco OSB 18 mm preservado; Caixão agora aceito por decisão de P5).
- **VALIDADO** pelos testes: utilitários de polígono, telhamento (cobertura/espessura equivalente/crista/avisos), paginação não convexa e filtros de fixador. `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (TC-058..TC-074 — telha recortada, enchimento, caixão, placas não convexas, fixadores); `docs\LAJE.md` (rev. 2) **ausente**; furos internos do contorno não são representáveis (`ContornoLaje` sem buracos) → documentado com `Aviso`, não modelado.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 ATUALIZAÇÃO — LOTE 2 DA LAJE: TRELIÇA, FIXADORES, PAGINAÇÃO, TELHA E ARGAMASSA 3D (2026-10-16)

Missão autônoma de desenvolvimento do módulo de lajes (Lote 2). Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, nenhum commit. O bloco anterior (§0.1) continua válido; o E2E no Revit 2021 segue PENDENTE.

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Treliça (vigas)**: `Domain\Laje\TrelicaTipo.cs`, `ComposicaoTrelica.cs` (`MembroTrelicaTipo`, `MembroTrelica`, `ComposicaoTrelica`, `PainelAlvoPadraoMm=600`, `AlturaPadraoMm=200`), `CatalogoTrelica.cs` (Warren/Pratt/Howe/DiagonaisAlternadas/MontantesVerticais, todos implementados) + `Geometry\TrelicaBuilder.cs` (banzos+montantes+diagonais por painel, `LarguraBarraPaineisMm=40`). `CatalogoTiposVigaLaje` marca Treliça implementado; **Caixão segue NÃO implementado**. `ParametrosLaje` ganhou `Trelica`/`AlturaTrelicaMm`/`NumeroPaineisTrelica`/`PainelTrelicaAlvoMm` + `AlturaVigaEfetivaMm()`; `CamadasLaje` usa a altura efetiva. UI: `PanelTrelica`/`ComboTrelicaTipo`/`TextAlturaTrelica`/`TextPaineisTrelica`. Persistência: `trelica=`, `alturaTrelica=`, `paineisTrelica=`, `painelAlvoTrelica=`. Prévia mostra a treliça.
  - **Fixadores/parafusos com nível de detalhe**: `Domain\Laje\FixadoresLaje.cs` (`NivelDetalheFixadores {Oculto,Simplificado,Detalhado}`, `TipoFixador {Estrutural,Placa}`, `PosicaoFixador`, `ResultadoFixadores`, `EspacamentoPlacaPadraoMm=300`) + `Geometry\FixadorBuilder.cs` (`CriarSolidosParafuso`, LOD simplificado/detalhado). Estruturais nas pontas das vigas sobre as guias; de placa ao longo das vigas. `PapelElementoLaje.Parafuso` + `PapelElemento.ParafusoLaje` (com cor). UI: `PanelFixadores`/`ComboFixadores`/`TextEspacamentoFixadores`. Persistência: `fixadores=`, `espFixadores=`.
  - **Paginação de placas**: `Domain\Laje\PaginacaoPlacas.cs` (chapas 1200 mm, `JuntaPadraoMm=3`, transposição alternada por camada, recorte Sutherland–Hodgman para contornos convexos; não convexo → placa única + aviso; `MaxPlacas=3000`). `LajeVigaBuilder.CriarSolidoPlaca` ganhou sobrecarga para lista de pontos (polígono). `ParametrosLaje.PaginarPlacas`/`JuntaPlacasMm` + validação. Persistência: `paginar=`, `juntaPlacas=`. UI: `CheckPaginar`/`TextJunta`.
  - **Telha TP40 3D**: `Domain\Laje\SecaoTelhaTp40.cs` (perfil ondulado puro da chapa) + `Geometry\TelhaBuilder.cs` (chapa ondulada extrusada ao longo das vigas; cobre o retângulo envolvente — recorte pelo contorno PENDENTE). `LajeGenerationService.CriarTelhaEAArgamassa` cria DirectShapes "LSF - Telha" (`PapelElementoLaje.Telha`, material `TelhaLaje`).
  - **Argamassa 3D**: mesma rotina cria a placa plana acima da onda (`PapelElementoLaje.Argamassa`, material `ArgamassaLaje`). O enchimento das ondas entre telhas NÃO é modelado (documentado).
  - **Resultado/prévia**: `ResultadoGeracaoLaje` ganhou `TelhasCriadas`/`ArgamassasCriadas` (somam em `TotalCriados`). `PreviaLaje` ganhou `ChapasPlacas`/`ChapasTelha` + linhas.
- **TESTADO** (via `powershell.exe`, WSL→Windows): `dotnet build LSFRevitPlugin.sln` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **345/345 aprovados**, 0 falhas, 0 ignorados. Novos testes: `LajeTrelicaTests`, `LajeFixadoresTests`, `LajePaginacaoTests`, `LajeTelhaTests` + casos em `LajePreviaTests`. Sem regressão (Caixão continua rejeitado; telha 0,65 continua lançando; `LajeCamadasTests` preservado).
- **VALIDADO** pelos testes: domínio de treliça, fixadores, paginação, perfil da telha e contagens da prévia; `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (TC-058..TC-072 — treliça, fixadores, chapas/juntas, telha e argamassa 3D); recorte da telha pelo contorno exato; enchimento das ondas (argamassa entre telhas); Caixão; engenharia dos valores provisórios; `docs\LAJE.md` (rev. 2) **ausente**.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 REFINAMENTO FUNCIONAL DA LAJE (2026-10-15)

Refinamento funcional do módulo de lajes (12 seções do pedido). Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, nenhum commit. O bloco anterior (§0.0) continua válido; o E2E no Revit 2021 segue PENDENTE.

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Catálogo de perfis de laje**: `Domain\Laje\CatalogoPerfisLaje.cs` — só Steel Frame, alma 90/140/200 mm e chapa 0,95/1,25 mm; exclui Drywall e 0,80 mm (`EhPerfilDeLaje`). `ParametrosLaje.Validar()` passa a rejeitar Drywall e chapa 0,80 na laje. `UI\LajeConfigWindow` usa esse recorte (esconde 0,80 e Drywall é tratado como inválido no domínio).
  - **Orientação sem duplicidade**: `Domain\Laje\OrientacaoHelper.cs` + enum `OrientacaoUI {Automatica, PorBordaReferencia, Manual}`. A janela passou de 4 para 3 opções + checkbox "Perpendicular à borda de referência"; enum/resolver originais (`OrientacaoVigasModo`/`ResolvedorOrientacaoVigas`) preservados.
  - **Telha**: `TelhaTp40.Espessura063Mm = 0,63` + `PesoKgM2Chapa063` (OrigemValor.Provisorio) em `EspessurasDisponiveisMm`; rejeição de 0,65 mantida; UI virou ComboBox com 0,43/0,50/0,63.
  - **Tipos de viga**: `Domain\Laje\CatalogoTiposVigaLaje.cs` (PerfilSimples e BackToBack **implementados**; Caixão e Treliça **não**, por falta de geometria/dados). `Domain\Laje\ComposicaoViga.cs` (puro) descreve os sub-perfis — BackToBack = 2 "C" costas-com-costas, deslocamento ±Aba/2, segundo espelhado — e a largura transversal (2×Aba). `StudSolidBuilder.CriarSolidoMontanteHorizontal` ganhou `deslocamentoTransversalInterno` e `abertoParaFrente`; `LajeVigaBuilder.CriarSolidosViga`. `LajeGenerationService` usa a largura transversal no `LayoutVigas` e cria **1 DirectShape por viga** com os sólidos da composição.
  - **Cobertura de placas**: `Domain\Laje\ComposicaoCobertura.cs` (OSB 15/16/22/25; cimentícia 10/20; OSB + cimentícia 10 — camadas separadas). `ParametrosLaje` ganhou `ComposicaoPlacas`, `EspessuraOSBMm`, `EspessuraCimenticiaMm`. `CamadasLaje.CalcularSecaComPlacas` empilha as placas; `CamadasLaje.Calcular` passa a usar a composição quando ≠ Nenhuma (senão mantém o piso do catálogo — **sem regressão**). `LajeVigaBuilder.CriarSolidoPlaca` (extrusão plana do contorno); a geração cria DirectShapes das placas com identidade `PapelElementoLaje.{CamadaOsb,CamadaCimenticia}` e cores `PapelElemento.{OsbLaje,CimenticiaLaje}`. **Telha/argamassa NÃO são geradas** (geometria ondulada pendente; documentado).
  - **Fluxo de criação**: `Core\LajeCommand.cs` oferece apenas **2 origens** — "Usar piso existente do Revit" e "Desenhar contorno da laje" (cliques; explica que o Revit 2021 não abre o esboço nativo a partir de um comando externo). Opção de linhas removida da UI (serviço/classe preservados).
  - **Botão temporário T11 removido** de `Core\App.cs`; `Core\LajeTesteCommand.cs` e testes preservados.
  - **Persistência**: `ConfiguracaoLajeTexto` grava/lê `composicao`, `osb`, `cimenticia` (e já `tipoViga`); textos antigos caem em padrões seguros. GUIDs de schema **inalterados**; nenhum schema novo.
- **TESTADO** (via `powershell.exe`, WSL→Windows): `dotnet build LSFRevitPlugin.sln` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **301/301 aprovados** (274 + 27 novos `LajeRefinamentoTests`), 0 falhas, 0 ignorados. Sem regressão.
- **VALIDADO** pelos testes: catálogo de perfis (sem drywall/0,80), orientação sem duplicidade, telha 0,63, composição de placas + validação + empilhamento de camadas, composição de viga pura, validação de tipo de viga e persistência. `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (TC-058..TC-067); geometria 3D da telha/argamassa; Caixão/Treliça; engenharia dos valores provisórios; `docs\LAJE.md` (rev. 2) **ausente**.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.2 LAJE (T11 → T14: config, origem, registro, criar/editar) (sessão anterior)

Sessão que fecha o fluxo utilizável da laje: configuração explícita, três origens de contorno, registro por documento e os comandos finais de criar/editar. O bloco da T11 logo abaixo (§0.0) continua válido; o E2E do blocking e da laje seguem PENDENTES.

- **IMPLEMENTADO** (código novo, compila):
  - **Domínio (puro, sem Revit DB)**: `Domain\Laje\SistemaLaje.cs`, `OrientacaoVigasModo.cs` (+`ResolvedorOrientacaoVigas`), `SegmentoContorno.cs`, `PreviaLaje.cs`, `OrigemContorno.cs`, `ConfiguracaoLajeTexto.cs`, `RegistroLajesTexto.cs`. `ContornoLaje.MontarDeSegmentos` (clusterização, grau-2, caminhamento de ciclo; erros de ramo/aberto/múltiplos laços) e `ToleranciaJuncaoMm=0.1`. `ParametrosLaje` ganhou `Sistema`, `ModoOrientacao`, `ArestaReferencia` + `DirecaoEfetivaGraus`.
  - **Serviços**: `Services\ResultadoContornoLaje.cs`, `ContornoDePisoService.cs` (topo do piso via `HostObjectUtils.GetTopFaces` + `GetEdgesAsCurveLoops`; laço externo = maior área; furos contados/avisados), `ContornoDeLinhasService.cs` (`CurveElement.GeometryCurve`/`LocationCurve`, só retas), `ContornoDePontosService.cs`. `RevitVersionAdapter.InternoParaMilimetros`.
  - **Persistência/regeneração (Lote E)**: `Services\RegistroLajesService.cs` — registro por DOCUMENTO no Extensible Storage do `ProjectInformation`, **schema novo** `LSFRegistroLajes`, **GUID novo** `2f7c9a10-6b3d-4e58-9a21-3c4d5e6f7081` (não reutiliza GUIDs). `Listar`/`ProximoLajeId`(maior+1)/`Salvar`/`Remover`. `LajeGenerationService.Gerar` agora recebe `TipoOrigemContorno`+`origemElementoId`, grava o registro na MESMA transação e faz **rollback** se nada for criado (restaura a laje anterior). Cálculo (parâmetros/layout/camadas) acontece ANTES da transação.
  - **UI (Lote D)**: `UI\LajeConfigWindow.xaml(.cs)` — sistema, tipo, perfil, telha, orientação, espaçamento, cota e PRÉVIA (sem gerar); só sistemas/tipos implementados; suporta `existente` para edição. `UI\EscolhaLajeWindow.cs` (code-only) para escolher a laje a editar.
  - **Commands (Lote F)**: `Core\LajeCommand.cs` ("Criar laje": piso/linhas/pontos → config → gera em `LajeId` novo), `Core\EditarLajeCommand.cs` ("Editar laje": lista, reabre preenchido, regenera no mesmo `LajeId`, relê o piso se ainda existir), `Core\LajeCommandUi.cs` (diálogo de resultado compartilhado). Botões registrados em `App.cs`; botão `LajeTesteCommand` (T11) mantido, marcado TEMPORÁRIO.
- **TESTADO** (via `powershell.exe`, WSL→Windows): `dotnet build LSFRevitPlugin.sln` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **274/274 aprovados** (238 + 36 novos), 0 falhas, 0 ignorados. Novos testes: `LajeSistemaTests`, `LajeOrientacaoModoTests`, `LajeContornoMontarTests`, `LajePreviaTests`, `LajeParametrosConfigTests`, `LajeConfiguracaoTextoTests` (inclui registro).
  - Nota: houve um jank no `RegistroLajesTexto` (CS1628: `out` usado em função local) → corrigido passando a acumular numa lista local e atribuir ao `out` no fim.
- **VALIDADO** pelos testes: domínio de laje (sistema/orientação/contorno/parâmetros/prévia) e codecs (round-trip e rejeição de texto inválido); `Domain` sem `Autodesk.Revit.DB`. Geometria/serviço/registro/commands NÃO validados (dependem do Revit).
- **PENDENTE**: (a) TC-058 e TC-059..TC-062 em `CHECKLIST.md` [PENDENTE] — E2E visual no Revit 2021 (criar por piso/linhas/pontos, editar, regenerar não duplica, 1 Ctrl+Undo); (b) contrato `docs\LAJE.md` (rev. 2) **ausente no repositório** → aderência a T09/T12/T14/T15..T20 NÃO confrontada (não inventar); (c) engenharia dos valores provisórios do catálogo.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.3 LAJE — T11 (sessão anterior)

Trabalho mais recente: **T11 da laje** (geometria das vigas de perfil simples + guias de borda + serviço de geração), preparando a primeira visualização no Revit 2021. O bloco do Lote 2 logo abaixo (§0.1) continua válido; o E2E do blocking segue PENDENTE.

- **IMPLEMENTADO** (código novo, compila):
  - `Domain\Laje\IdentidadeLaje.cs` (codec puro: `PapelElementoLaje {Viga, GuiaBorda}`, `IdentidadeLaje`, `LajeTexto.Prefixo = "LSF-LAJE | "`, `IdentidadeLajeCodec.MontarTexto/TentarExtrair`).
  - `Services\PertencenciaLajeService.cs` (Extensible Storage, schema `LSFPertencenciaLajes`, **GUID NOVO** `a7bdd95e-e83e-4f7b-83eb-56c897218bdd`, campos LajeId/Tipo/Papel/Numero; NÃO reutiliza GUID existente).
  - `Geometry\LajeVigaBuilder.cs` (`CriarSolidoViga` via `StudSolidBuilder.CriarSolidoMontanteHorizontal` com `dirSecaoPerp = XYZ.BasisZ` → **alma VERTICAL**, aba transversal; `CriarSolidoGuiaBorda` reutiliza `SecaoGuia.ContornoU` com eixos mapeados: V→Z (alma em pé), Z→horizontal-para-dentro).
  - `Services\LajeGenerationService.cs` + `Services\ResultadoGeracaoLaje.cs` (ParametrosLaje→OrientacaoVigas→LayoutVigas→CamadasLaje; **UMA TransactionGroup**; apaga a laje anterior por `LajeId` antes de regerar → regenerar NÃO duplica; avisos/erros no resultado; mede uma viga com `LeitorDeEstrutura.Medir`; recuo de meia aba nas vigas de borda aplicado na geometria).
  - `Geometry\LsfMateriais.cs` estendido: `PapelElemento.{VigaLaje,GuiaBordaLaje}` + cases de cor (Drywall verde/verde-escuro; SteelFrame roxo/roxo-escuro). `MateriaisLsf`/`ObterConjunto` **NÃO mudaram** (a laje cria seus materiais via `ObterOuCriar`), preservando a geração da parede.
  - `Core\LajeTesteCommand.cs` + botão em `Core\App.cs` "Laje (teste T11)" — **TEMPORÁRIO** (substituído pela T14); necessário para o teste real (a medição com `LeitorDeEstrutura.Medir` só é observável via UI).
  - `Tests\...\Laje\LajeIdentidadeTests.cs` (9 testes do codec).
- **TESTADO** (via `powershell.exe`, WSL→Windows): `dotnet build LSFRevitPlugin.sln -v minimal` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **238/238 aprovados** (229 + 9), 0 falhas, 0 ignorados. Sem regressão.
- **VALIDADO**: pelos testes, `IdentidadeLajeCodec` (round-trip e rejeição do prefixo da parede) e `Domain` sem `Autodesk.Revit.DB`. A geometria/serviço NÃO foram validados (dependem do Revit).
- **PENDENTE**: (a) TC-058 em `CHECKLIST.md` [PENDENTE] — E2E visual no Revit 2021 (alma vertical, guia nas pontas, regerar não duplica, 1 Ctrl+Z, medição comprimento=4000 mm / alma=90 mm); (b) contrato `docs\LAJE.md` (rev. 2) **ausente no repositório** (texto veio colado na conversa) → T11 não confrontada com T09/T12/T14/T15..T20; (c) a identidade mínima aqui cobre só o essencial da T11, NÃO a T09 completa.
- Detalhe do Lote 1/2: `docs\LAJE_REFERENCIA_E_LOTE1.md`, seção 10.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.4 LAJE — LOTE 2 (sessão anterior)

As seções de blocking abaixo (1 a 18) continuam válidas — o E2E do blocking no Revit 2021 segue PENDENTE — mas o trabalho mais recente desta sessão foi a integração do **Lote 2 da laje** em `Domain\Laje\`.

- **IMPLEMENTADO** (código integrado, compila): `Domain\Laje\LayoutVigas.cs`, `OrientacaoVigas.cs`, `ResultadoLayoutVigas.cs`, `CamadasLaje.cs` + 3 testes (`Tests\...\Laje\LajeLayoutVigasTests.cs`, `LajeCamadasTests.cs`, `LajeOrientacaoVigasTests.cs`). O Lote 1 (160 testes) foi preservado.
- **TESTADO** (executado no Windows via `powershell.exe`): `dotnet build LSFRevitPlugin.sln -v minimal` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **229/229 aprovados** (160 do Lote 1 + 69 do Lote 2), 0 falhas, 0 ignorados. Sem regressão. (Os 4 avisos CS8625 do helper de teste foram eliminados com `double[]? apoios`, sem alterar testes.)
- **VALIDADO** (evidência disponível): layout, alinhamento, colisão determinística, bordas fixas, segmentos em contorno côncavo, guias de borda, orientação automática/manual e camadas — comprovados pelos testes; `Domain` sem referência a `Autodesk.Revit.DB`.
- **PENDENTE**: (a) contrato `docs\LAJE.md` (rev. 2) **ausente** no repositório e em artefatos/histórico acessível → T01..T20 e aderência à rev. 2 NÃO validados (não inventar); (b) decisão de bordas fixas implementada/testada, mas o confronto com o contrato rev. 2 fica pendente; (c) integração no pipeline de geração (Geometry/Services/UI) e demais lotes da laje NÃO existem; (d) engenharia dos valores provisórios do catálogo (Lote 1) pendente.
- Detalhe completo da laje: `docs\LAJE_REFERENCIA_E_LOTE1.md`, seção 10.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 1. IDENTIFICAÇÃO
- Data: 2026-10-13
- Branch: master
- Commit atual: 79365b4 (HEAD -> master) feat: read plate panel identity from extensible storage
- Estado Git: Nenhum commit desta sessão. Working tree com alterações não commitadas pré-existentes + features de blocking (arquivos novos não versionados). Nada foi resetado/limpo.

## 2. OBJETIVO ATUAL
8ª tarefa do dia (continuação do blocking). A 7ª tarefa (dobra da seção inteira, já validada VISUALMENTE pelo usuário no Revit — "a geometria atual da dobra está correta") deixou dois pontos:

**(a) CORRIGIR o COMPRIMENTO e as COORDENADAS das extremidades do blocking** para que as ALMAS das extremidades dobradas ENCOSTEM nos montantes (sem lacuna, sem sobreposição indevida), usando as FACES REAIS dos montantes. A dobra NÃO deve ser refeita (visualmente aprovada); corrigir apenas posição/comprimento.

**(b) ADICIONAR a distribuição ALTERNADA das fileiras de blocking**: padrão "Todos os montantes" (comportamento atual) + "Alternado (sim/não)", com opção de INVERTER o montante inicial. Ordenar montantes por posição espacial estável; preservar alturas das fileiras; evitar duplicações; manter compatibilidade das configurações existentes. NÃO reintroduzir "Montante adicional".

Build/testes EXECUTADOS nesta sessão via `powershell.exe` (WSL → Windows): `dotnet build LSFRevitPlugin.sln -v minimal` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **120/120 aprovados** (net48). **NÃO declarar resolvido**: falta APENAS a validação visual/E2E no Revit 2021 do NOVO comprimento e da distribuição alternada.

## 3. ETAPA ATUAL
Código editado para a 8ª tarefa:
1. **Correção de comprimento/coordenadas (`BlockingCalculator`)** — sem tocar em `GeometriaLocal`: `PontoInicial = faceA − dir*d`, `PontoFinal = faceB + dir*d` e `ComprimentoAoLongoEixo = compEntreFaces + 2*d`, com `d = BlockingTrackBuilder.ProfundidadePernaBlockingMm` (= 60 mm). Com isso a LINHA DE DOBRA cai exatamente na face interna do montante (a peça recua uma profundidade de perna para fora da face) e a perna abraça o montante; o corpo horizontal íntegro fica exatamente entre as faces internas. Mantido `minCompInterna` (120 mm) sobre o vão entre faces.
2. **Distribuição alternada**: `BlockingConfig.Distribuicao` (`DistribuicaoBlocking.Todos` = padrão | `Alternado`) + `BlockingConfig.AlternarInvertido`; `PreferenciasGeracao.BlockingDistribuicao`/`BlockingAlternarInvertido` + `CriarBlockingConfig`; UI (`OpcoesGeracaoWindow.xaml(.cs)`: radios "Todos os montantes"/"Alternado" + checkbox "Inverter o montante inicial"); aviso do `BlockingService`; filtro no `BlockingCalculator` por paridade do índice do montante inicial (ordem estável por posição no eixo) e testes (paridade/inversão).
3. Docs `.continuity` atualizados.

## 4. ESTADO ATUAL
Código editado (BlockingCalculator.cs, BlockingConfig.cs, PreferenciasGeracao.cs, OpcoesGeracaoWindow.xaml/.cs, BlockingService.cs, BlockingCalculatorTests.cs, BlockingTrackBuilderTests.cs + 3 arquivos .continuity). Compilação e testes EXECUTADOS nesta sessão via powershell.exe no Windows: build **0 avisos/0 erros**, testes **120/120 aprovados**. Nenhum commit/push feito.

## 5. ÚLTIMA ALTERAÇÃO RELEVANTE
Sessão 2026-10-13 (8ª tarefa do dia): CORREÇÃO do comprimento/coordenadas do blocking (linha de dobra na face do montante; blank = vão entre faces + 2×profundidade de perna) + ADIÇÃO da distribuição alternada (com inversão do montante inicial). A GEOMETRIA da dobra (7ª tarefa) foi PRESERVADA (validada visualmente).

### Correção de comprimento (BlockingCalculator) — 8ª tarefa
- `PontoInicial = faceA − dirEixo*d` e `PontoFinal = faceB + dirEixo*d` (antes eram as próprias faces), com `d = BlockingTrackBuilder.ProfundidadePernaBlockingMm = 60 mm`.
- `ComprimentoAoLongoEixo = compEntreFaces + 2*d` (antes `compEntreFaces`). O blank da guia passa a se estender uma profundidade de perna para FORA de cada face interna.
- Efeito na geometria (builder inalterado): corpo horizontal `Alma`/`AbaReta` fica exatamente entre as faces internas (`U=[d,L−d]` do blank ⇒ faces); cada perna (seção inteira dobrada 90° para baixo) ocupa o vão do próprio montante — a LINHA DE DOBRA coincide com a face interna do montante. Sem lacuna nem sobreposição indevida.
- `Centro` (meio entre faces) e a cota absoluta inalterados; `minCompInterna` (120 mm) continua aplicado sobre `compEntreFaces`.

### Distribuição alternada — 8ª tarefa
- `DistribuicaoBlocking { Todos, Alternado }` (enum novo em `BlockingConfig.cs`); padrão `Todos` (comportamento histórico).
- `BlockingConfig.Distribuicao` + `BlockingConfig.AlternarInvertido` (default false). `Validar()` normaliza valor inválido para `Todos`.
- `BlockingCalculator`: quando `Alternado`, mantém apenas os vãos cujo montante inicial tem a paridade escolhida (`i % 2 == (AlternarInvertido ? 1 : 0)`); `i` é a posição na lista do painel JÁ ordenada por posição no eixo da parede (ordem espacial estável). Assim os vãos alternados não compartilham montante (evita duplicar pernas).
- `PreferenciasGeracao`: `BlockingDistribuicao`/`BlockingAlternarInvertido` + wiring em `CriarBlockingConfig`.
- UI: radios `RadioBlockingTodos`/`RadioBlockingAlternado` + checkbox `ChkBlockingAlternarInverter` em `PanelBlocking`; carga (`SelecionarDistribuicao`) e gravação (`DistribuicaoEscolhida`).
- `BlockingService`: aviso indicando a distribuição usada.

### Geometria (BlockingTrackBuilder) — 7ª tarefa (PRESERVADA)
- A seção continua sendo a GUIA "U" (8 vértices, sem lábios), deitada, alma na horizontal.
- CORPO HORIZONTAL (`Alma` + `AbaReta`): secao "U" integra de uma LINHA DE DOBRA a outra — `U=[d, L-d]`, sendo `d = ProfundidadePernaBlockingMm` (= 60 mm, PROVISÓRIO). Alma `Z∈[0,t]`, flanges `Z∈[t,aba]`, faixa externa `|V| = meioAlma..meiaLargura`. Nada do corpo é cortado (não há "abas" no corpo).
- PERNAS (`PernaAlma` + `PernaAba`): nas duas extremidades, a SEÇÃO INTEIRA (alma + 2 abas) é dobrada 90° PARA BAIXO na linha de dobra (U=d no início; U=L−d no fim), formando perna vertical que abraça o montante — boca da "U" voltada para o montante:
  - Perna de início: `PernaAlma` `U=[d−t,d]` (placa vertical, espessura da chapa em U), `Z=[−d,0]`; `PernaAba` `U=[d−aba,d−t]`, `Z=[−d,0]`, `|V|=meioAlma..meiaLargura` (2, uma por face).
  - Perna de fim (espelho): `PernaAlma` `U=[L−d,L−d+t]`; `PernaAba` `U=[L−d+t,L−d+aba]`.
  - Alma e abas dobram JUNTAS; perfil "U" preservado na perna; corpo e perna compartilham apenas a face/linha de dobra (continuidade SEM lacuna nem sobreposição); perna NÃO é peça separada nem orelha.
- Constante NOVA: `ProfundidadePernaBlockingMm = 60` (PROVISÓRIO) = descida da perna; a dobra só existe quando `L >= 2*d`; vão menor → guia "U" cheia sem pernas.
- LIMITAÇÃO EXPLÍCITA: o raio/linha de dobra seria um vinco vivo; o canto entre alma e aba na região dobrada fica VAZIO (corresponde ao ENTALHE DE ALÍVIO não definido no projeto — NÃO modelado); fixação e resistência NÃO dimensionadas.
- Enum `PapelBlockingTrecho`: agora `Alma`, `AbaReta`, `PernaAlma`, `PernaAba` (removido `AbaExtremidade`).
- Testes comprovam: seção de guia "U" (não montante); dobra da seção inteira nas duas extremidades; alma e abas dobram juntas na MESMA linha de dobra; descida `Z=[−d,0]` de todas as pernas; alma da perna vira placa vertical (extensão U = chapa); abas da perna nas duas faces (+V/−V) com perfil preservado (extensão U = aba−chapa); boca da U voltada para o montante; corpo integral entre as linhas de dobra; continuidade na linha de dobra SEM sobreposição de volume entre trechos; nada fora do vão/nem acima da flange; vão curto → guia cheia sem pernas; posição/alturas/fileiras mantidos.

### Configurações (após a 7ª tarefa)
- Opção "Montante adicional adjacente ao existente" **REMOVIDA** (código e UI). `BlockingConfig` não tem mais `MontanteAdicional`; a face/offset fica como era SEM o adicional (extremidade-inicio +aba, extremidade-fim −aba, regular ±aba/2).
- Demais opções PRESERVADAS: Habilitado, Fileiras, Finalidade, AlturasRelativasCm, AlinharJuntasPlacas, AlturasJuntasPlacasCm; checkbox/estado/preferências correspondentes.

### Testes
- `BlockingTrackBuilderTests` REESCRITOS (14) para a geometria DA DOBRA DA SEÇÃO INTEIRA (ver §5 acima).
- `BlockingCalculatorTests`: REMOVIDOS os 2 testes de `MontanteAdicional`; demais inalterados.

## 6. ARQUIVOS RELEVANTES (desta sessão)
- LSFRevitPlugin/Geometry/BlockingCalculator.cs (8ª tarefa: `PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `ComprimentoAoLongoEixo=compEntreFaces+2d`; filtro de distribuição alternada; docs de PosicaoBlocking atualizados)
- LSFRevitPlugin/Domain/Models/BlockingConfig.cs (8ª tarefa: enum `DistribuicaoBlocking` + `Distribuicao`/`AlternarInvertido`; `Validar` normaliza)
- LSFRevitPlugin/Core/PreferenciasGeracao.cs (8ª tarefa: `BlockingDistribuicao`/`BlockingAlternarInvertido` + `CriarBlockingConfig`)
- LSFRevitPlugin/UI/OpcoesGeracaoWindow.xaml + .xaml.cs (8ª tarefa: radios de distribuição + checkbox de inversão; carga/salva/config)
- LSFRevitPlugin/Services/BlockingService.cs (8ª tarefa: aviso da distribuição; aviso da dobra mantido)
- Tests/.../BlockingCalculatorTests.cs (8ª tarefa: comprimento/linha de dobra atualizados + 4 testes de distribuição alternada)
- Tests/.../BlockingTrackBuilderTests.cs (8ª tarefa: expectativas de PontoInicial/Final/Comprimento atualizadas)
- Referência (7ª tarefa, economia): LSFRevitPlugin/Geometry/BlockingTrackBuilder.cs (`GeometriaLocal`, `ProfundidadePernaBlockingMm=60`; PRESERVADO nesta sessão)

## 7. TESTES E VALIDAÇÕES (resultados desta sessão)
- Build: `powershell.exe -NoProfile -Command "cd C:\lsf_revit_plugin; dotnet build LSFRevitPlugin.sln -v minimal"` → **0 Avisos, 0 Erros** (EXECUTADO nesta sessão).
- Testes: `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "cd C:\lsf_revit_plugin; & .\rodar-testes.ps1"` → **120/120 aprovados** (net48; 0 falhas, 0 ignorados) — EXECUTADO nesta sessão. (116 anteriores + 4 novas asserções de distribuição: padrão/paridade/inversão/repetição.)
- Revit 2021 E2E: NÃO executado (WSL não roda o Revit). Validação visual MANUAL no Revit 2021 é OBRIGATÓRIA (versões anteriores falharam justamente na validação visual).

## 8. DECISÕES CONFIRMADAS
- Blocking é GUIA/TRACK em "U" (sem lábios), material de guia; NÃO montante.
- DESCRIÇÃO MAIS RECENTE (usuário, CANCELA a 6ª tarefa): ao encontrar cada montante, a SEÇÃO INTEIRA da guia (ALMA + 2 ABAS JUNTAS) é cortada e dobrada 90° PARA BAIXO, formando perna vertical que abraça o montante; linha de dobra colada na face do montante; corpo integral entre as duas linhas de dobra; perna é a CONTINUAÇÃO da própria seção (sem peças/orelhas separadas).
- Só UMA configuração de extremidade agora (a opção "Montante adicional" foi REMOVIDA a pedido do usuário).
- 8ª tarefa: a LINHA DE DOBRA fica colada na FACE INTERNA do montante; o blank se estende uma profundidade de perna (60 mm) para fora de cada face (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `Comprimento=compEntreFaces+2d`); corpo horizontal exatamente entre as faces.
- 8ª tarefa: distribuição padrão = TODOS os vãos entre montantes adjacentes; opção ALTERNADA (vãos de paridade escolhida, ordem estável por posição) com INVERTER o montante inicial. Config compatível (defaults preservam o comportamento antigo).
- Posicionamento (faces internas), cota absoluta, múltiplas fileiras e opções (finalidade/alturas/juntas) inalterados.
- Nada em Domain/ referencia Autodesk.Revit.DB.

## 9. DECISÕES PROVISÓRIAS (LIMITAÇÕES EXPLÍCITAS)
- `ProfundidadePernaBlockingMm = 60` (mm) é PROVISÓRIO = descida da perna vertical da dobra. Perfis/peças diferentes devem ser revisados em engenharia e ajustados após validação visual.
- O raio/linha de dobra seria um vinco vivo (SEM material na curva modelado); o canto entre alma e aba na região dobrada fica VAZIO (corresponde ao ENTALHE DE ALÍVIO não definido no projeto — NÃO modelado); parafusos NÃO definidos. Fixação estrutural NÃO dimensionada.
- FITA METÁLICA (elemento independente) NÃO foi modelada.
- Vão curto demais (`L < 2*d`): guia "U" cheia sem pernas (cai com segurança, sem sobreposição).

## 10. QUESTÕES EM ABERTO
- Confirmar visualmente no Revit 2021 (OBRIGATÓRIO): as pernas dobradas agora ENCOSTAM nos montantes (a dobra da seção inteira já foi aprovada visualmente na 7ª tarefa; o que muda é o COMPRIMENTO/coordenadas).
- Confirmar visualmente no Revit 2021 a distribuição ALTERNADA (e a inversão do montante inicial).
- Definir/validar em engenharia: raio/linha de dobra, entalhe de alívio, profundidade da perna, parafusos.

## 11. PENDÊNCIAS
- Validação visual E2E no Revit 2021 do NOVO comprimento e da distribuição alternada — OBRIGATÓRIA antes de declarar resolvido.
- Engenharia da dobra (dimensões provisórias → definitivas).
- Fase 12: travamento interno completo (2 guias + 1 montante por nó).
- Itens pendentes de ROADMAP.md Fases 8-12 e Fase 13.

## 12. NÃO FAZER
- Alterar paginação/cortes/juntas/sobras/geometria validada dos demais elementos
- Remover Comments legado
- Reutilizar GUIDs
- Git reset/revert/clean destrutivo
- Misturar contexto NexoBIM
- Commit/push sem solicitação explícita
- Declarar o blocking resolvido sem build/testes no Windows e sem validação visual no Revit 2021
- Inventar especificação de parafusos ou declarar a ligação estrutural dimensionada
- REGRESSÕES PROIBIDAS: retornos de FLANGE dobrados para dentro ("AbaDobrada", 4ª tarefa); abas cortadas DA ALMA ou alma cortada (5ª tarefa); PONTAS de flange dobradas PARA FORA como abas independentes (6ª tarefa — SUBSTITUÍDA pela dobra da seção inteira); corpo horizontal com a alma NÃO dobrada onde a seção inteira deveria dobrar; peças/orelhas separadas; "orelha" acima das flanges
- Modelar a fita metálica sem pedido explícito
- PRECAUÇÃO: não reintroduzir a opção "Montante adicional" (removida a pedido do usuário nesta sessão)

## 13. DEPENDÊNCIAS E AMBIENTE
- Revit 2021, .NET Framework 4.8, C# 8.0
- MSBuild/dotnet 9 (Windows), VS 2022 Community, xUnit/vstest
- WSL/OpenCode (sem dotnet; build/testes apenas via powershell.exe no Windows)

## 14. PROBLEMAS E RISCOS
- BAIXO: build/testes desta tarefa JÁ executados nesta sessão (0/0 e 120/120); código compila limpo.
- MÉDIO/ALTO: o NOVO comprimento/coordenadas (pernas encostando nos montantes) e a distribuição alternada NÃO foram validados visualmente (SÓ se valida no Revit 2021; risco de nova iteração).
- MÉDIO: `ProfundidadePernaBlockingMm=60` e a relação "linha de dobra colada na face do montante" ainda precisam de validação/engenharia; a correção assume que a "alma da extremidade dobrada" deve coincidir com a face interna do montante.
- BAIXO: blocking não persistido em arquivo (comportamento pré-existente).

## 15. PRÓXIMA AÇÃO EXATA
1. (FEITO nesta sessão) Windows: `dotnet build LSFRevitPlugin.sln -v minimal` → 0 avisos/0 erros; `.\rodar-testes.ps1` → 120/120 aprovados.
2. Abrir Revit 2021 e "Ativar LSF" numa parede de teste (Linha de Localização = centro). Marcar "Gerar blocking horizontal interno", definir alturas (ex.: `100; 200`) e gerar. Conferir que as PERNAS dobradas agora ENCOSTAM nos montantes (sem lacuna), com o corpo horizontal integro entre as faces internas. Depois escolher "Alternado" e conferir que só os vãos alternados recebem blocking; marcar "Inverter o montante inicial" e conferir o deslocamento da paridade. Ler os avisos no TaskDialog.

## 16. CONTEXTO PARA NOVA IA
LSF Plugin Revit puro. HEAD 79365b4 (sem novos commits). Blocking = guia em "U" (sem lábios) com DOBRA DA SEÇÃO INTEIRA nas extremidades (7ª tarefa, GEOMETRIA VALIDADA VISUALMENTE; CANCELA a 6ª/5ª/4ª): CORPO horizontal `Alma` `U=[d,L−d]`, `Z∈[0,t]` + `AbaReta` `U=[d,L−d]`, `Z∈[t,aba]`; PERNAS de início `PernaAlma` `U=[d−t,d]` + `PernaAba` `U=[d−aba,d−t]`, `Z=[−d,0]`, e de fim (espelho) — 90° PARA BAIXO, perfil U preservado, boca da U voltada para o montante. Constante provisória `ProfundidadePernaBlockingMm = 60` (`temPerna = d>0 && L>=2d`; senão guia cheia). 8ª tarefa: o `BlockingCalculator` agora passa `PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `ComprimentoAoLongoEixo=compEntreFaces+2d` (d=60mm), de modo que a LINHA DE DOBRA cai EXATAMENTE na face interna do montante e a perna o abraça (corpo exatamente entre as faces). 8ª tarefa também adiciona `DistribuicaoBlocking {Todos(padrão), Alternado}` + `AlternarInvertido` (vãos alternados por paridade do índice do montante, ordem estável por posição; UI/preferências/aviso). OPÇÃO "Montante adicional adjacente ao existente" REMOVIDA — NÃO reintroduzir. Geometria em função pura `BlockingTrackBuilder.GeometriaLocal` (TrechoBlocking: Alma/AbaReta/PernaAlma/PernaAba); material de guia; posição/alturas preservadas. LIMITAÇÃO: canto alma×aba na região dobrada fica vazio (entalhe de alívio não definido). FEITO: build 0/0 e testes 120/120 no Windows (via powershell.exe). PENDENTE: E2E no Revit 2021 (obrigatório) do novo comprimento e da distribuição alternada + engenharia da dobra. Preservar arquitetura: Domain sem referência Revit DB. Não modelar fita metálica nem dimensionar a fixação estrutural.

## 17. ÚLTIMA SESSÃO
- Tarefa: (a) CORRIGIR o comprimento/coordenadas do blocking para que as almas das extremidades dobradas encostem nos montantes (linha de dobra na face interna; sem refazer a geometria já validada); (b) ADICIONAR distribuição alternada (com inversão do montante inicial).
- Ação: leitura de `BlockingCalculator`/`BlockingTrackBuilder`/`BlockingConfig`/`BlockingService`/`PreferenciasGeracao`/`OpcoesGeracaoWindow`/`StructureGenerationService`/testes; correção do comprimento (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `Comprimento=compEntreFaces+2d`); enum `DistribuicaoBlocking` + `Distribuicao`/`AlternarInvertido` + filtro por paridade; UI (radios + checkbox); preferências; aviso do serviço; atualização/adição de testes no calculator e no builder; 3 arquivos .continuity sincronizados.
- Resultado: código editado; build **0/0** e testes **120/120** EXECUTADOS nesta sessão via `powershell.exe` (Windows); sem commit.
- Ponto de parada: validação visual no Revit 2021 (obrigatória) do novo comprimento e da distribuição alternada; engenharia da dobra.

## 18. REGRA DE SINCRONIZAÇÃO DE CONTINUIDADE

Regra permanente: sempre que atualizar, criar ou modificar `.continuity/CURRENT_STATE.md` devido a uma mudança relevante no estado do projeto, também atualizar imediatamente `.continuity/CHATGPT_HANDOFF.md` e `.continuity/CURRENT_HANDOFF.md`, mantendo os três sincronizados, refletindo o estado real e verificável para permitir retomada em nova conversa.