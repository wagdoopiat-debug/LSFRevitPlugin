# CHATGPT HANDOFF

## 0. MISSÃO ATUAL — LAJE: CORREÇÕES 1–4 + LOTE C/D/E (travamento, fitas, fixadores com direção, alinhamento a montantes) (2026-10-18)

Missão autônoma com 12 requisitos (Lotes A–E). Lote A/B e as 4 correções dirigidas já estavam feitos; **esta sessão** conclui o Lote C (travamento das trelicas, fitas tensionadas, fixadores com direção) e o Lote D (alinhamento de vigas internas a montantes). Aditivo/integrativo; **nenhum reset/limpeza/revert**, **nenhum commit**. Build/testes no Windows via `powershell.exe`.

- **DECISÕES DO USUÁRIO**: (C) geométrico de partida + **documentar** (PROVISÓRIO/NÃO NORMATIVO, sem inventar valores); (D) **ler paredes do Revit sob a laje**.
- **IMPLEMENTADO**: `Domain\Laje\TravamentoLaje.cs` (membro de borda: trelica de travamento p/ viga trelicada, guia "U" p/ demais; apoios); `Domain\Laje\FitasTensionadasLaje.cs` + `Geometry\FitaBuilder.cs` (opt-in, passo 1500 provisório); `ParametrosLaje.FitasTensionadas`/`EspacamentoFitasTensionadasMm` + persistência; `DirecaoFixador {Vertical, AoLongoDaViga}` + `FixadorBuilder` por direção; `Services\ApoiosLajeService.cs` + `LayoutVigas` (alinhamento a montantes, tolerância 50mm, aviso quando não há montante); `PreviaLaje`/`LajeConfigWindow`/commands propagam o provider; novos `PapelElementoLaje {Travamento, Fita}` e `PapelElemento {TravamentoLaje, FitaLaje}`; UI `CheckFitasTensionadas`/`PanelFitas`.
- **TESTADO**: **414/414 aprovados**, 0 falhas; build **0 avisos / 0 erros**. Novos/atualizados: `LajeTravamentoTests`, `LajeFixadoresTests`, `LajePreviaTests`, `LajeLayoutVigasTests`, `LajeIdentidadeTests`. `Domain` sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (Lote C/D); dimensionamento estrutural das fitas/travamento; `docs\LAJE.md` rev. 2 ausente; divergências TP40/perfil 980 documentadas como provisórias.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 MISSÃO — LAJE LOTE A+B (A1-A5 + B1-B4: regras fixas, UI, perfis C trelica, montagem, caixão, gusset) (2026-10-16)

Nova missão autônoma consolidando testes reais do Revit 2021 em 12 requisitos, organizados em Lotes A–E. Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, **nenhum commit**. Lote A e Lote B completos; E2E no Revit 2021 segue PENDENTE (Revit aberto → usar `bin_ci` redirect).

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Lote A (A1–A5)**: paginação obrigatória (removidas propriedades `PaginarPlacas`/`JuntaPlacasMm` de `ParametrosLaje` e chaves `paginar`/`juntaPlacas` de `ConfiguracaoLajeTexto`); orientação OSB perpendicular / cimentícia paralela via `PaginacaoPlacas.DimensoesOrientadas` + `EhCamadaOSB`; Drywall removido da UI de laje; janela redimensionável; esquema vetorial da trelica no CanvasTrelica.
  - **Lote B1**: `Geometry/TrelicaBuilder.cs` reescrito — diagonais e montantes usam a MESMA seção "C" com aba enrijecida do perfil (`PerfilSpec.AbaMm/LarguraAlmaMm/EspessuraChapaMm/LabioMm`) via `StudSolidBuilder.CriarSolidoMontanteHorizontal`.
  - **Lote B2**: `ComposicaoViga` ganhou enums `MontagemTrelica {Encaixada, BackToBack}` + `SubPerfisMontagem()` + `LarguraTransversalMm(tipo, perfil, montagem)`; `ParametrosLaje.MontagemTrelica`; UI `ComboMontagemTrelica`; persistência `montagemTrelica=`.
  - **Lote B3**: Caixão com duas configurações (`ComposicaoCaixaoTipo {FrenteAFrente, Encaixados}`) via `ComposicaoViga.SubPerfis(tipo, perfil, caixao)`; `LajeVigaBuilder.CriarSolidosViga` sobrecarga com caixão; UI `PanelCaixao`/`ComboComposicaoCaixao`; persistência `composicaoCaixao=`.
  - **Lote B4**: chapas gusset opcionais. `Domain/Laje/GussetLaje.cs` (novo, puro) + `Geometry/GussetBuilder.cs` (novo); `ParametrosLaje.GussetPlacas` (padrão false); `PapelElementoLaje.Gusset` + `PapelElemento.GussetLaje` + `ResultadoGeracaoLaje.ChapasGussetCriadas`; UI `CheckGussetPlacas`; persistência `gussetPlacas=`. Valores geométricos de partida (80×80×1.5mm) — não dimensionados estruturalmente.
  - **Testes**: `LajeGussetTests` (novo, 2 testes); `LajePaginacaoTests`/`LajeParametrosTests`/`LajeConfiguracaoTextoTests`/`LajePreviaTests` atualizados para a A1 (paginação obrigatória).
- **TESTADO** (via `powershell.exe`, WSL→Windows, `.\rodar-testes.ps1`): **375/375 aprovados**, 0 falhas, 0 ignorados. Build `dotnet build LSFRevitPlugin.sln -p:OutputPath=bin_ci\` → **0 avisos / 0 erros**. Sem regressão (telha 0,65 lança; piso seco OSB 18 mm preservado; Drywall rejeitado na laje).
- **VALIDADO** pelos testes: paginação obrigatória/junta fixa 3mm, orientação OSB/cimentícia, parâmetros de laje, persistência backward compat, prévia, gusset em nós da trelica. `Domain` segue sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E visual no Revit 2021 (TC-058..TC-074, incl. TC-066B..TC-066E); Lote C (bracing/fasteners), Lote D (coordenação geométrica/cobertura), Lote E (integração/validação completa + relatório final); `docs\LAJE.md` (rev. 2) ausente; furos internos do contorno não representáveis.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 MISSÃO ANTERIOR — LAJE P1..P5 (recorte da telha, enchimento, paginação não convexa, caixão, fixadores) (2026-10-16)

Missão autônoma do módulo de lajes com 6 prioridades (P1..P6). Aditivo/integrativo; **nenhum reset/commit**. Bloco §0.1 (Lote 2) válido; E2E Revit 2021 PENDENTE.

- **P4** não convexo: `PaginacaoPlacas` delega a `RecortePoligono` (triangula + recorta por célula + costura arestas); sem "placa única". **`RecortePoligono.cs` (novo, puro)**: `Contem`/`IntervalosV`/`Recortar`/`TriangulosConvexos`/`RecortarConvexosPorCelula`/`Costurar`/`Triangular`.
- **P5** Caixão: `ComposicaoViga` Caixão (2 "C" frente a frente, largura = Aba); `CatalogoTiposVigaLaje.Caixao.Implementado=true`.
- **P1** telha recortada: `SecaoTelhaTp40.PerfilInferior`; novo `TelhamentoTp40.cs` (faixas em u); `TelhaBuilder` reescrito (`CriarSolidoDaFaixa`), com avisos.
- **P2** enchimento: `TelhamentoTp40.CalcularEnchimento`; `ParametrosLaje.PreencherOndas` + UI `CheckPreencherOndas`; `PapelElementoLaje.Enchimento`.
- **P3** fixadores: descarte fora do contorno (`Contem`) + tests.
- **TESTADO**: `dotnet test -p:OutputPath=bin_ci\` (Revit aberto trava a DLL) → **366/366**, 0 falhas. Novos `LajeRecortePoligonoTests`, `LajeTelhamentoTests`. Sem regressão.
- **PENDENTE**: E2E Revit 2021; `docs\LAJE.md` rev. 2 ausente; furos internos do contorno não representáveis.
- **Nenhum commit/push.** HEAD `79365b4`.

## 0.1 ATUALIZAÇÃO — LOTE 2 DA LAJE: TRELIÇA, FIXADORES, PAGINAÇÃO, TELHA E ARGAMASSA 3D (2026-10-16)

Missão autônoma de desenvolvimento do módulo de lajes (Lote 2). Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, nenhum commit. O bloco anterior (§0.1) continua válido; o E2E no Revit 2021 segue PENDENTE.

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Treliça**: `Domain\Laje\TrelicaTipo.cs`, `ComposicaoTrelica.cs`, `CatalogoTrelica.cs` (Warren/Pratt/Howe/DiagonaisAlternadas/MontantesVerticais) + `Geometry\TrelicaBuilder.cs`; `ParametrosLaje.Trelica`/`AlturaTrelicaMm`/`NumeroPaineisTrelica`/`PainelTrelicaAlvoMm` + `AlturaVigaEfetivaMm()`; treliça aceita e Caixão rejeitado; UI `PanelTrelica`; persistência `trelica=`/`alturaTrelica=`/`paineisTrelica=`/`painelAlvoTrelica=`.
  - **Fixadores**: `Domain\Laje\FixadoresLaje.cs` (`NivelDetalheFixadores`, `TipoFixador`, `PosicaoFixador`) + `Geometry\FixadorBuilder.cs`; `PapelElementoLaje.Parafuso` + `PapelElemento.ParafusoLaje`; UI `PanelFixadores`; persistência `fixadores=`/`espFixadores=`.
  - **Paginação de placas**: `Domain\Laje\PaginacaoPlacas.cs` (chapas 1200 mm, juntas de 3 mm, transposição alternada, recorte Sutherland–Hodgman para convexos; não convexo = placa única + aviso); `ParametrosLaje.PaginarPlacas`/`JuntaPlacasMm`; UI `CheckPaginar`/`TextJunta`; persistência `paginar=`/`juntaPlacas=`.
  - **Telha e argamassa 3D**: `Domain\Laje\SecaoTelhaTp40.cs` + `Geometry\TelhaBuilder.cs` + `LajeGenerationService.CriarTelhaEAArgamassa` (DirectShapes "LSF - Telha"/"LSF - Argamassa"; telha cobre o retângulo envolvente — recorte pelo contorno pendente; enchimento das ondas NÃO modelado).
  - **Resultado/prévia**: `ResultadoGeracaoLaje.TelhasCriadas`/`ArgamassasCriadas`; `PreviaLaje.ChapasPlacas`/`ChapasTelha`.
- **TESTADO** (via `powershell.exe`, WSL→Windows): build → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **345/345 aprovados**, 0 falhas, 0 ignorados. Novos: `LajeTrelicaTests`, `LajeFixadoresTests`, `LajePaginacaoTests`, `LajeTelhaTests` + casos em `LajePreviaTests`. Sem regressão.
- **VALIDADO** pelos testes: domínio de treliça, fixadores, paginação, perfil da telha e contagens da prévia; `Domain` sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E no Revit 2021 (TC-058..TC-072); recorte da telha pelo contorno; enchimento das ondas; Caixão; engenharia dos provisórios; `docs\LAJE.md` rev. 2 ausente.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.1 REFINAMENTO FUNCIONAL DA LAJE (2026-10-15)

Refinamento funcional do módulo de lajes (12 seções do pedido). Trabalho aditivo/integrativo; **nenhum reset/limpeza/revert**, nenhum commit. O bloco anterior (§0.0) continua válido; o E2E no Revit 2021 segue PENDENTE.

- **IMPLEMENTADO** (código novo/integrado, compila):
  - **Catálogo de perfis de laje**: `Domain\Laje\CatalogoPerfisLaje.cs` — só Steel Frame, alma 90/140/200 mm e chapa 0,95/1,25 mm; exclui Drywall e 0,80 mm. `ParametrosLaje.Validar()` rejeita Drywall/0,80 na laje. UI usa esse recorte.
  - **Orientação sem duplicidade**: `Domain\Laje\OrientacaoHelper.cs` + enum `OrientacaoUI {Automatica, PorBordaReferencia, Manual}`. Janela passou de 4 para 3 opções + checkbox "Perpendicular à borda de referência"; enum/resolver originais preservados.
  - **Telha**: `TelhaTp40.Espessura063Mm = 0,63` + peso provisório em `EspessurasDisponiveisMm`; rejeição de 0,65 mantida; UI vira ComboBox (0,43/0,50/0,63).
  - **Tipos de viga**: `CatalogoTiposVigaLaje.cs` (PerfilSimples e BackToBack implementados; Caixão/Treliça não). `ComposicaoViga.cs` (BackToBack = 2 "C" costas-com-costas, desloc. ±Aba/2, segundo espelhado; largura = 2×Aba). `StudSolidBuilder` ganhou `deslocamentoTransversalInterno`/`abertoParaFrente`; `LajeVigaBuilder.CriarSolidosViga`; `LajeGenerationService` usa largura transversal no layout e cria 1 DirectShape por viga.
  - **Cobertura de placas**: `ComposicaoCobertura.cs` (OSB 15/16/22/25; cimentícia 10/20; OSB+cimentícia — camadas separadas). `ParametrosLaje` ganhou `ComposicaoPlacas`/`EspessuraOSBMm`/`EspessuraCimenticiaMm`. `CamadasLaje.CalcularSecaComPlacas` empilha; `Calcular` usa composição quando ≠ Nenhuma (senão mantém piso do catálogo — sem regressão). `LajeVigaBuilder.CriarSolidoPlaca`; DirectShapes das placas com `PapelElementoLaje.{CamadaOsb,CamadaCimenticia}` e cores `PapelElemento.{OsbLaje,CimenticiaLaje}`. **Telha/argamassa NÃO geradas.**
  - **Fluxo de criação**: `Core\LajeCommand.cs` oferece só **2 origens** — "Usar piso existente do Revit" e "Desenhar contorno da laje" (cliques; Revit 2021 não abre esboço nativo por comando externo). Opção de linhas removida da UI (serviço/classe preservados).
  - **Botão temporário T11 removido** de `Core\App.cs`; `LajeTesteCommand.cs` e testes preservados.
  - **Persistência**: `ConfiguracaoLajeTexto` grava/lê `composicao`/`osb`/`cimenticia` (e já `tipoViga`); textos antigos caem em padrões seguros. GUIDs inalterados; nenhum schema novo.
- **TESTADO** (via `powershell.exe`, WSL→Windows): build → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **301/301 aprovados** (274 + 27 novos `LajeRefinamentoTests`), 0 falhas, 0 ignorados. Sem regressão.
- **VALIDADO** pelos testes: catálogo de perfis, orientação, telha 0,63, composição/validação/empilhamento de camadas, composição de viga, validação de tipo de viga e persistência. `Domain` sem `Autodesk.Revit.DB`.
- **PENDENTE**: E2E no Revit 2021 (TC-058..TC-067); telha/argamassa 3D; Caixão/Treliça; engenharia dos provisórios; `docs\LAJE.md` rev. 2 ausente.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.2 LAJE (T11 → T14: config, origem, registro, criar/editar) (sessão anterior)

Sessão que fecha o fluxo utilizável da laje: configuração explícita, três origens de contorno, registro por documento e os comandos finais de criar/editar. O bloco da T11 logo abaixo (§0.0) continua válido; o E2E do blocking e da laje seguem PENDENTES.

- **IMPLEMENTADO** (código novo, compila):
  - **Domínio (puro, sem Revit DB)**: `Domain\Laje\SistemaLaje.cs`, `OrientacaoVigasModo.cs` (+`ResolvedorOrientacaoVigas`), `SegmentoContorno.cs`, `PreviaLaje.cs`, `OrigemContorno.cs`, `ConfiguracaoLajeTexto.cs`, `RegistroLajesTexto.cs`. `ContornoLaje.MontarDeSegmentos` (clusterização, grau-2, caminhamento de ciclo; erros de ramo/aberto/múltiplos laços) e `ToleranciaJuncaoMm=0.1`. `ParametrosLaje` ganhou `Sistema`, `ModoOrientacao`, `ArestaReferencia` + `DirecaoEfetivaGraus`.
  - **Serviços de origem**: `ResultadoContornoLaje.cs`, `ContornoDePisoService.cs` (topo do piso via `HostObjectUtils.GetTopFaces` + `GetEdgesAsCurveLoops`; laço externo = maior área; furos contados/avisados), `ContornoDeLinhasService.cs` (`CurveElement.GeometryCurve`/`LocationCurve`, só retas), `ContornoDePontosService.cs` (min 3, cota = média dos Z). `RevitVersionAdapter.InternoParaMilimetros`.
  - **Persistência/regeneração**: `RegistroLajesService.cs` — registro por DOCUMENTO no Extensible Storage do `ProjectInformation`, **schema novo** `LSFRegistroLajes`, **GUID novo** `2f7c9a10-6b3d-4e58-9a21-3c4d5e6f7081` (não reutiliza GUIDs). `Listar`/`ProximoLajeId`(maior+1)/`Salvar`/`Remover`. `LajeGenerationService.Gerar` recebe `TipoOrigemContorno`+`origemElementoId`, grava o registro na MESMA transação e faz **rollback** se nada for criado (restaura a laje anterior); cálculo ocorre ANTES da transação.
  - **UI**: `UI\LajeConfigWindow.xaml(.cs)` (sistema, tipo, perfil, telha, orientação, espaçamento, cota e PRÉVIA sem gerar; só sistemas/tipos implementados; aceita `existente` para edição) e `UI\EscolhaLajeWindow.cs` (code-only).
  - **Commands**: `Core\LajeCommand.cs` ("Criar laje": piso/linhas/pontos → config → gera em `LajeId` novo), `Core\EditarLajeCommand.cs` ("Editar laje": escolhe registro, reabre config preenchida, regenera no MESMO `LajeId`; relê o contorno do piso se ele ainda existir), `Core\LajeCommandUi.cs` (diálogo de resultado compartilhado); botões registrados em `App.cs`; botão temporário da T11 mantido.
- **TESTADO** (via `powershell.exe`, WSL→Windows): build → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **274/274 aprovados** (268 → +6 desta sessão), 0 falhas, 0 ignorados. Sem regressão.
- **VALIDADO** pelos testes: domínio de laje (sistema/orientação/contorno/parâmetros/prévia) e codecs (`ConfiguracaoLajeTexto`, `RegistroLajesTexto`: round-trip e rejeição de texto inválido); `Domain` sem `Autodesk.Revit.DB`. Geometria/serviço/registro/commands NÃO validados (dependem do Revit).
- **PENDENTE**: (a) TC-058 e TC-059..TC-062 em `CHECKLIST.md` [PENDENTE] — E2E visual no Revit 2021 (criar por piso/linhas/pontos, editar, regenerar não duplica, 1 Ctrl+Undo); (b) contrato `docs\LAJE.md` (rev. 2) **ausente no repositório** → aderência a T09/T12/T14/T15..T20 NÃO confrontada (não inventar); (c) engenharia dos valores provisórios do catálogo.
- Detalhe da T11: `docs\LAJE_REFERENCIA_E_LOTE1.md`, seção 10.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.3 LAJE — T11 (sessão anterior)

Trabalho mais recente: **T11 da laje** (geometria das vigas de perfil simples + guias de borda + serviço de geração), preparando a primeira visualização no Revit 2021. O bloco do Lote 2 logo abaixo (§0.1) continua válido; o E2E do blocking segue PENDENTE.

- **IMPLEMENTADO** (código novo, compila): `Domain\Laje\IdentidadeLaje.cs` (codec puro: `PapelElementoLaje {Viga, GuiaBorda}`, `LajeTexto.Prefixo = "LSF-LAJE | "`); `Services\PertencenciaLajeService.cs` (schema `LSFPertencenciaLajes`, **GUID NOVO** `a7bdd95e-e83e-4f7b-83eb-56c897218bdd`); `Geometry\LajeVigaBuilder.cs` (`CriarSolidoViga` com `dirSecaoPerp = XYZ.BasisZ` → **alma VERTICAL**; `CriarSolidoGuiaBorda` reutiliza `SecaoGuia.ContornoU` com eixos mapeados V→Z e Z→horizontal-para-dentro); `Services\LajeGenerationService.cs` + `Services\ResultadoGeracaoLaje.cs` (ParametrosLaje→OrientacaoVigas→LayoutVigas→CamadasLaje; **UMA TransactionGroup**; apaga a laje anterior por `LajeId` antes de regerar → não duplica; avisos/erros no resultado; mede uma viga com `LeitorDeEstrutura.Medir`; recuo de meia aba nas bordas); `Geometry\LsfMateriais.cs` estendido com os papeis `PapelElemento.{VigaLaje,GuiaBordaLaje}` e as cores (Drywall verde/verde-escuro; SteelFrame roxo/roxo-escuro) — `MateriaisLsf`/`ObterConjunto` NAO mudaram (a laje cria seus materiais via `ObterOuCriar`), preservando a geracao da parede; `Core\LajeTesteCommand.cs` + botão temporário em `App.cs` "Laje (teste T11)" (necessário para o teste real; substituído pela T14); `Tests\...\Laje\LajeIdentidadeTests.cs` (9 testes).
- **TESTADO** (via `powershell.exe`): build → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **238/238 aprovados** (229 + 9), 0 falhas. Sem regressão.
- **VALIDADO**: `IdentidadeLajeCodec` por testes (round-trip; rejeita prefixo da parede) e `Domain` sem `Autodesk.Revit.DB`. Geometria/serviço NÃO validados (dependem do Revit).
- **PENDENTE**: (a) TC-058 [PENDENTE] — E2E visual no Revit 2021; (b) `docs\LAJE.md` (rev. 2) **ausente no repo** (texto colado na conversa) → T11 não confrontada com T09/T12/T14/T15..T20; (c) a identidade mínima cobre só o essencial da T11, NÃO a T09 completa.
- Detalhe do Lote 1/2: `docs\LAJE_REFERENCIA_E_LOTE1.md`, seção 10.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 0.4 LAJE — LOTE 2 (sessão anterior)

As seções de blocking abaixo (1 a 24) continuam válidas — o E2E do blocking no Revit 2021 segue PENDENTE — mas o trabalho mais recente desta sessão foi a integração do **Lote 2 da laje** em `Domain\Laje\`.

- **IMPLEMENTADO** (código integrado, compila): `Domain\Laje\LayoutVigas.cs`, `OrientacaoVigas.cs`, `ResultadoLayoutVigas.cs`, `CamadasLaje.cs` + 3 testes (`Tests\...\Laje\LajeLayoutVigasTests.cs`, `LajeCamadasTests.cs`, `LajeOrientacaoVigasTests.cs`). Lote 1 (160 testes) preservado.
- **TESTADO** (executado no Windows via `powershell.exe`): build → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **229/229 aprovados** (160 + 69), 0 falhas. Sem regressão. CS8625 do helper resolvido com `double[]? apoios`, sem alterar testes.
- **VALIDADO** (evidência disponível): layout/alinhamento/colisão/bordas/segmentos/guias/orientação/camadas comprovados pelos testes; `Domain` sem `Autodesk.Revit.DB`.
- **PENDENTE**: (a) contrato `docs\LAJE.md` (rev. 2) **ausente** → T01..T20 e aderência à rev. 2 não validados (não inventar); (b) bordas fixas implementadas/testadas, confronto com o contrato rev. 2 pendente; (c) integração no pipeline de geração e demais lotes da laje inexistentes; (d) engenharia dos valores provisórios do catálogo (Lote 1).
- Detalhe completo: `docs\LAJE_REFERENCIA_E_LOTE1.md`, seção 10.
- **Nenhum commit/push.** HEAD segue `79365b4`.

## 1. PROJETO

- Nome: LSF Plugin Revit
- Localização: /mnt/c/lsf_revit_plugin
- Exclusivo: Sim — não misturar com NexoBIM

## 2. OBJETIVO ATUAL

Continuação do blocking (8ª tarefa). A 7ª tarefa (dobra da SEÇÃO INTEIRA, aprovada VISUALMENTE no Revit) deixou dois pontos:

1. **CORRIGIR o COMPRIMENTO e as COORDENADAS das extremidades** para que as almas das extremidades dobradas ENCOSTEM nos montantes (sem lacuna, sem sobreposição indevida), usando as FACES REAIS dos montantes. A geometria da dobra NÃO deve ser refeita (aprovada visualmente); corrigir só posição/comprimento.
2. **ADICIONAR a distribuição ALTERNADA**: padrão "Todos os montantes" + "Alternado (sim/não)" com INVERTER o montante inicial. Ordenar montantes por posição espacial estável; preservar alturas; evitar duplicações; manter compatibilidade. NÃO reintroduzir "Montante adicional".

> Blocking = GUIA horizontal em "U"; ao encontrar cada montante, a SEÇÃO INTEIRA (alma + as duas abas JUNTAS) é cortada e dobrada 90° PARA BAIXO, formando perna vertical que ABRAÇA o montante; corpo horizontal íntegro entre as linhas de dobra.

As imagens em `docs/references/blocking/` têm extensão duplicada e NÃO são legíveis por esta IA.

## 3. ETAPA ATUAL

Código da 8ª tarefa editado: (a) `BlockingCalculator` corrigido (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `ComprimentoAoLongoEixo=compEntreFaces+2d`, d=`ProfundidadePernaBlockingMm`=60 mm) — a LINHA DE DOBRA cai na face interna do montante e a perna o abraça; (b) distribuição alternada (`DistribuicaoBlocking`, `BlockingConfig.Distribuicao`/`AlternarInvertido`, UI, preferências, aviso, testes). `BlockingTrackBuilder.GeometriaLocal` (7ª tarefa) PRESERVADA. Build/testes **EXECUTADOS nesta sessão** via `powershell.exe` (WSL → Windows): build **0 avisos/0 erros** e testes **120/120 aprovados** (net48). Sem commit. **NÃO declarar resolvido** — falta a validação visual E2E no Revit 2021 do novo comprimento e da distribuição alternada, e a engenharia da dobra.

## 4. ONDE PARAMOS

Após a 7ª tarefa em `Geometry/BlockingTrackBuilder` (correção do detalhe de extremidade):
- Seção: GUIA "U" (8 vértices, sem lábios), material de guia.
- CORPO HORIZONTAL (1 só peça, alma + 2 abas): de uma LINHA DE DOBRA à outra — `U=[d, L−d]`, `d = ProfundidadePernaBlockingMm` (= 60 mm PROVISÓRIO). `Alma`: `|V| ≤ meiaLargura`, `Z∈[0,t]`. `AbaReta`: faixa externa `|V| = meioAlma..meiaLargura`, `Z∈[t,aba]`. Nada do corpo é cortado.
- PERNAS (dois lados), SEÇÃO INTEIRA dobrada 90° PARA BAIXO na linha de dobra (U=d no início; U=L−d no fim), boca da "U" voltada para o montante:
  - Início: `PernaAlma` `U=[d−t,d]`, `Z=[−d,0]`; `PernaAba` `U=[d−aba,d−t]`, `Z=[−d,0]`, `|V|=meioAlma..meiaLargura` (2, uma por face).
  - Fim (espelho): `PernaAlma` `U=[L−d,L−d+t]`; `PernaAba` `U=[L−d+t,L−d+aba]`.
  - Alma e abas dobram JUNTAS; perfil "U" preservado na perna; corpo/perna compartilham a linha de dobra (continuidade sem lacuna nem sobreposição); perna NÃO é peça separada/orelha; nada acima da flange.
- Constante NOVA: `ProfundidadePernaBlockingMm = 60` (PROVISÓRIO) = descida da perna; a dobra só existe quando `L >= 2*d`; vão menor → guia "U" cheia sem pernas.
- LIMITAÇÃO EXPLÍCITA: vinco vivo (sem raio); o canto alma×aba na região dobrada fica VAZIO (corresponde ao ENTALHE DE ALÍVIO não definido — NÃO modelado); fixação/resistência NÃO dimensionadas.
- Enum `PapelBlockingTrecho`: `Alma`, `AbaReta`, `PernaAlma`, `PernaAba` (removido `AbaExtremidade`).
- **REMOVIDA a opção "Montante adicional"** (`BlockingConfig.MontanteAdicional`, `PreferenciasGeracao.BlockingMontanteAdicional`, checkbox `ChkBlockingMontanteAdicional` no XAML + code-behind, ramo `offA/offB += aba` no `BlockingCalculator`, aviso no `BlockingService`, e os 2 testes `Calcular_MontanteAdicional_*`). A face/offset das extremidades fica SEM o adicional (extremidade-inicio +aba, extremidade-fim −aba, regular ±aba/2).

**8ª tarefa (comprimento + distribuição alternada):**
- `BlockingCalculator`: `PontoInicial = faceA − dirEixo*d`, `PontoFinal = faceB + dirEixo*d`, `ComprimentoAoLongoEixo = compEntreFaces + 2*d` (d = `ProfundidadePernaBlockingMm` = 60 mm). A linha de dobra coincide com a face interna do montante e a perna o abraça; o corpo fica exatamente entre as faces. `Centro`/cota inalterados; `minCompInterna` (120 mm) segue sobre `compEntreFaces`. Docs de `PosicaoBlocking` atualizados.
- `BlockingConfig`: enum `DistribuicaoBlocking { Todos, Alternado }` + `Distribuicao` (default `Todos`) e `AlternarInvertido` (default false); `Validar` normaliza valor inválido.
- Filtro: em `Alternado` mantém só os vãos cujo montante inicial tem a paridade escolhida (`i % 2 == (AlternarInvertido ? 1 : 0)`), `i` = posição na lista do painel já ordenada por posição no eixo.
- `PreferenciasGeracao`: `BlockingDistribuicao`/`BlockingAlternarInvertido` + `CriarBlockingConfig`.
- `OpcoesGeracaoWindow.xaml(.cs)`: radios `RadioBlockingTodos`/`RadioBlockingAlternado` + checkbox `ChkBlockingAlternarInverter`; carga `SelecionarDistribuicao`, gravação `DistribuicaoEscolhida`.
- `BlockingService`: aviso da distribuição.
- Testes: `BlockingCalculatorTests` (linha de dobra nas faces + comprimento com 2d + 4 de distribuição: padrão, alternado, inversão, sem repetição) e `BlockingTrackBuilderTests` (expectativas de `PontoInicial/Final/Comprimento`).

Nenhum commit (HEAD 79365b4).

## 5. ÚLTIMA AÇÃO

- 4ª tarefa (referência histórica): "retorno de FLANGE (AbaDobrada) dobrado para dentro" — SUBSTITUÍDA.
- 5ª tarefa (referência histórica): "abas de extremidade da ALMA; alma em `U=[t,L-t]`" — SUBSTITUÍDA.
- 6ª tarefa (referência histórica): "pontas das FLANGES dobradas 90° PARA FORA além da meiaLargura" — SUBSTITUÍDA.
- 7ª tarefa (referência): o usuário descreveu a construção correta (seção inteira alma+abas dobrada 90° para baixo, perna abraçando o montante). Redesenho da `GeometriaLocal` (corpo `U=[d,L−d]` + pernas `PernaAlma/PernaAba`). GEOMETRIA VALIDADA VISUALMENTE no Revit.
- 8ª tarefa (ESTA): (a) o usuário pediu CORRIGIR o comprimento/coordenadas para as pernas encostarem nos montantes (sem refazer a dobra, já aprovada); (b) ADICIONAR distribuição alternada com inversão do montante inicial. Correção no `BlockingCalculator` (`faceA−d`/`faceB+d`/`compEntreFaces+2d`); enum/`Distribuicao`/`AlternarInvertido`; UI/preferências/aviso; testes do calculator (comprimento + 4 de distribuição) e do builder (expectativas).

## 6. RESULTADO REAL (desta sessão)

- CONCLUÍDO (código):
  - `BlockingCalculator`: `PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `ComprimentoAoLongoEixo=compEntreFaces+2d` (d=`ProfundidadePernaBlockingMm`=60 mm). Linha de dobra na face interna do montante; perna abraçando o montante; corpo exatamente entre as faces. `minCompInterna` (120 mm) segue sobre `compEntreFaces`.
  - `BlockingConfig`: enum `DistribuicaoBlocking { Todos, Alternado }` + `Distribuicao`/`AlternarInvertido`; `Validar` normaliza.
  - Filtro de distribuição alternada por paridade do índice do montante inicial (lista do painel ordenada por posição no eixo).
  - `PreferenciasGeracao`, `OpcoesGeracaoWindow.xaml(.cs)`, `BlockingService` (aviso) atualizados.
  - PRESERVADO: `BlockingTrackBuilder.GeometriaLocal` (7ª tarefa, validada visualmente), `ProfundidadePernaBlockingMm=60`.
  - Testes: `BlockingCalculatorTests` (linha de dobra nas faces + comprimento com 2d + 4 de distribuição) e `BlockingTrackBuilderTests` (expectativas de posição/comprimento).
- BUILD: EXECUTADO nesta sessão via `powershell.exe` (WSL → Windows) → **0 avisos/0 erros**.
- TESTES: EXECUTADOS nesta sessão (`.\rodar-testes.ps1`) → **120/120 aprovados**, 0 falhas, 0 ignorados (net48).
- NÃO EXECUTADO: validação E2E no Revit 2021 (ambiente WSL não roda o Revit).
- NÃO FEITO (por decisão): raio/linha de dobra, entalhe (relief), parafusos, dimensionamento da ligação; canto alma×aba na dobra fica vazio; fita metálica não modelada.

## 7. CONCLUÍDO

- 8ª tarefa: o blank do blocking se estende uma profundidade de perna (60 mm) para fora de cada face interna (`faceA−d`/`faceB+d`), de modo que a LINHA DE DOBRA cai na face do montante e a perna o abraça (corpo exatamente entre as faces), sem lacuna/sobreposição indevida.
- 8ª tarefa: distribuição ALTERNADA (vãos de paridade escolhida, ordem estável) + INVERTER o montante inicial; padrão `Todos` preserva o comportamento antigo.
- Extremidades do blocking = a SEÇÃO INTEIRA da guia (alma + 2 abas) dobrada 90° PARA BAIXO formando perna vertical que abraça o montante; corpo horizontal integral entre as duas linhas de dobra (7ª tarefa, validada visualmente).
- Guarda degenerada (vão curto → guia "U" cheia sem pernas).
- Opção "Montante adicional adjacente ao existente" REMOVIDA (a pedido do usuário) — NÃO reintroduzir.
- Posição/cota/múltiplas fileiras/finalidade/alturas/juntas inalterados; arquitetura Domain sem Revit.DB preservada.

## 8. EM ANDAMENTO

Nenhum. (Build/testes no Windows e validação no Revit 2021 são passos pendentes/manuais.)

## 9. PENDENTE

- Validação visual E2E no Revit 2021 (OBRIGATÓRIA): pernas dobradas ENCOSTANDO nos montantes (novo comprimento) e distribuição ALTERNADA (com inversão do montante inicial).
- Engenharia da dobra: raio/linha, entalhe de alívio, profundidade da perna, parafusos; validar `ProfundidadePernaBlockingMm=60`.
- Fase 12 completa (2 guias + 1 montante por nó). Itens ROADMAP.md Fases 8-12 / Fase 13.

## 10. NÃO CONFIRMADO

- Fabricação/ligação do blocking: NÃO validadas (dobra esquemática, vinco vivo; entalhe de alívio não definido).
- Build/testes da correção: JÁ executados (0/0 e 120/120); compila limpo.
- Aparência e posição finais no Revit 2021 do NOVO comprimento (pernas encostando) e da distribuição alternada (versões anteriores reprovaram; conferir a perna dobrada).
- Profundidade da perna (`60 mm`) e a hipótese de que a "alma da extremidade dobrada" deve coincidir com a face interna do montante.

## 11. PRÓXIMA AÇÃO EXATA

1. (FEITO nesta sessão) No Windows: `cd C:\lsf_revit_plugin; dotnet build LSFRevitPlugin.sln -v minimal` → 0 avisos/0 erros; `& .\rodar-testes.ps1` → 120/120 aprovados.
2. Abrir Revit 2021 e uma parede de teste (Linha de Localização = centro da parede)
3. Botão "Ativar LSF"; marcar "Gerar blocking horizontal interno"
4. Definir alturas (ex.: `100; 200`) e gerar; conferir que as PERNAS dobradas ENCOSTAM nos montantes (sem lacuna) e o corpo fica exatamente entre as faces
5. Escolher "Alternado" e marcar "Inverter o montante inicial"; conferir a paridade dos vãos; ler os avisos do TaskDialog (dobra/vínco NÃO validado); testar finalidade e "Alinhar as juntas horizontais das placas"
6. Se algo estiver errado, conferir `BlockingCalculator` (`PontoInicial/PontoFinal/Comprimento`), `BlockingTrackBuilder.GeometriaLocal` (layout/dobra) e a origem das alturas (BlockingRowsResolver / JuntasPlacasService)

## 12. ARQUIVOS RELEVANTES

- Desta sessão (8ª tarefa):
  - LSFRevitPlugin/Geometry/BlockingCalculator.cs (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `Comprimento=compEntreFaces+2d`; filtro de distribuição alternada; docs de `PosicaoBlocking`)
  - LSFRevitPlugin/Domain/Models/BlockingConfig.cs (enum `DistribuicaoBlocking` + `Distribuicao`/`AlternarInvertido`; `Validar`)
  - LSFRevitPlugin/Core/PreferenciasGeracao.cs (`BlockingDistribuicao`/`BlockingAlternarInvertido` + `CriarBlockingConfig`)
  - LSFRevitPlugin/UI/OpcoesGeracaoWindow.xaml + .xaml.cs (radios de distribuição + checkbox de inversão; carga/gravação)
  - LSFRevitPlugin/Services/BlockingService.cs (aviso da distribuição)
  - Tests/.../BlockingCalculatorTests.cs (linha de dobra nas faces + comprimento com 2d + 4 testes de distribuição)
  - Tests/.../BlockingTrackBuilderTests.cs (expectativas de `PontoInicial/Final/Comprimento`)
  - Preservado (7ª tarefa): LSFRevitPlugin/Geometry/BlockingTrackBuilder.cs (`GeometriaLocal` + `ProfundidadePernaBlockingMm=60`)
- Sessões mais antigas (referência, inalteradas): Geometry/SecaoGuia.cs, Geometry/SecaoMontante.cs, Geometry/GuiaBuilder.cs, Geometry/StudSolidBuilder.cs, Geometry/StudDirectShapeFactory.cs, Services/StructureGenerationService.cs, Services/JuntasPlacasService.cs, Domain/Models/BlockingRowsResolver.cs, Core/Command.cs (só consome `janelaOpcoes.Blocking`)
- Continuidade: .continuity/CURRENT_STATE.md, .continuity/CHATGPT_HANDOFF.md, .continuity/CURRENT_HANDOFF.md (sincronizados)

## 13. TESTES E VALIDAÇÕES (resultados desta sessão)

- Build: EXECUTADO nesta sessão: `powershell.exe -NoProfile -Command "cd C:\lsf_revit_plugin; dotnet build LSFRevitPlugin.sln -v minimal"` → **0 Avisos, 0 Erros**.
- Testes: EXECUTADOS nesta sessão: `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "cd C:\lsf_revit_plugin; & .\rodar-testes.ps1"` → **120/120 aprovados** (net48; 0 falhas, 0 ignorados). (116 anteriores + 4 testes de distribuição alternada.)
- E2E Revit 2021: PENDENTE (manual).

## 14. DECISÕES CONFIRMADAS

- Blocking é guia/track "U" (sem lábios) e usa material de guia; não é montante "C".
- DESCRIÇÃO MAIS RECENTE do usuário (CANCELA a 6ª tarefa): ao encontrar cada montante, a SEÇÃO INTEIRA da guia (ALMA + AS DUAS ABAS JUNTAS) é cortada e dobrada 90° PARA BAIXO, formando uma perna vertical que ABRAÇA o montante; linha de dobra colada na face do montante; corpo integral entre as duas linhas de dobra; NÃO são criadas peças/orelhas separadas.
- Só UMA configuração de extremidade agora (a opção "Montante adicional" foi REMOVIDA a pedido do usuário).
- 8ª tarefa: LINHA DE DOBRA colada na FACE INTERNA do montante (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `Comprimento=compEntreFaces+2d`, d=60 mm); a perna abraça o montante e o corpo fica exatamente entre as faces.
- 8ª tarefa: distribuição padrão = TODOS os vãos; opção ALTERNADA com INVERTER o montante inicial (vãos de paridade escolhida, ordem estável por posição). Config compatível (defaults = comportamento antigo).
- Posicionamento (faces internas), cota absoluta e alturas (explícitas > juntas > fileiras) inalterados.
- Finalidade independente do formato da peça; blocking desabilitado = nada criado.
- Arquitetura: Domain sem referência a Autodesk.Revit.DB.

## 15. DECISÕES PROVISÓRIAS

- `ProfundidadePernaBlockingMm = 60` (mm) = descida da perna vertical da dobra; PROVISÓRIO, revisar em engenharia após validação visual.
- Dobra modelada como placa reta dobrada (vinco vivo, sem raio/linha); o canto alma×aba na região dobrada fica VAZIO (entende-se como ENTALHE DE ALÍVIO não definido no projeto — NÃO modelado); parafusos NÃO definidos; fixação estrutural NÃO dimensionada.
- Vão curto (`L < 2*d`) → guia "U" cheia sem pernas.
- Preferências de blocking em memória (static); persistência em arquivo não alterada.

## 16. DECISÕES SUBSTITUÍDAS

- 4ª tarefa: "Retorno = trecho final da FLANGE (AbaDobrada) dobrado 90° PARA DENTRO abraçando o montante" → SUBSTITUÍDA.
- 5ª tarefa: "Abas de extremidade formadas pela ALMA (cortada e dobrada 90° para cima); alma em `U=[t,L-t]`" → SUBSTITUÍDA.
- 6ª tarefa: "Abas de extremidade = PONTAS das FLANGES dobradas 90° PARA FORA em V além da meiaLargura; alma e flanges íntegras (`U=[0,L]`)" → SUBSTITUÍDA.
- Enum `PapelBlockingTrecho.AbaExtremidade` → REMOVIDO (agora `PernaAlma`/`PernaAba` para a perna da dobra da seção inteira).
- Constante `DobraAbaExtremidadeMm` → REMOVIDA (agora `ProfundidadePernaBlockingMm = 60`).
- (Regressões antigas "orelhas soltas", "vão de 45mm sem aba" e "alma encurtada" permanecem proibidas.)

## 17. QUESTÕES EM ABERTO

- Confirmar no Revit 2021 (obrigatório) a aparência/posição da perna dobrada encostando nos montantes e a distribuição alternada.
- Definir/validar a geometria real da dobra (engenharia): raio, entalhe de alívio, profundidade da perna, parafusos.
- Persistir preferências de blocking em arquivo (futuro, opcional).

## 18. NÃO FAZER

- Alterar paginação/cortes/juntas/sobras/geometria validada dos demais elementos
- Remover Comments legado
- Reutilizar GUIDs entre schemas
- Git reset/revert/clean destrutivo
- Misturar contexto NexoBIM
- Commit/push automático (nenhum commit foi feito)
- Declarar resolvido sem build/testes no Windows e sem validação no Revit 2021
- Inventar especificação de parafusos ou declarar a ligação estrutural dimensionada
- REGRESSÕES: retornos de flange PARA DENTRO (AbaDobrada, 4ª); abas cortadas da ALMA ou alma cortada `U=[t,L-t]` (5ª); PONTAS de FLANGE dobradas PARA FORA como abas independentes (6ª, SUBSTITUÍDA); corpo horizontal com a alma NÃO dobrada onde a seção inteira deveria dobrar; peças/orelhas separadas; "orelha" acima das flanges
- Modelar a fita metálica sem pedido explícito
- PRECAUÇÃO: não reintroduzir a opção "Montante adicional" (removida a pedido do usuário)

## 19. DEPENDÊNCIAS E AMBIENTE

- Revit 2021 (.NET Framework 4.8, C# 8.0)
- MSBuild/dotnet 9 (Windows) + VS 2022 Community, xUnit/vstest
- WSL/OpenCode (sem dotnet; build/testes chamados via powershell.exe no Windows)

## 20. PROBLEMAS E RISCOS

- BAIXO: build/testes desta correção JÁ executados (0/0 e 120/120); código compila limpo.
- MÉDIO/ALTO: o NOVO comprimento (pernas encostando nos montantes) e a distribuição alternada ainda não foram validados visualmente no Revit 2021 (versões anteriores reprovaram ali).
- MÉDIO: `ProfundidadePernaBlockingMm=60` e a hipótese "linha de dobra colada na face do montante" (o blank recua `d` para fora de cada face) precisam de validação/engenharia.
- BAIXO: blocking sem persistência em arquivo (pré-existente).

## 21. GIT

- Branch: master
- Commit atual: 79365b4 (HEAD -> master) — nenhum commit novo
- Nesta sessão: apenas edição de working tree + .continuity (não commitado); arquivos de blocking continuam untracked
- Não fazer commit/push sem pedido explícito

## 22. CONTEXTO NECESSÁRIO PARA NOVA CONVERSA

Projeto LSF Plugin Revit puro. HEAD 79365b4. Blocking horizontal = guia/track "U" (sem lábios) com DOBRA DA SEÇÃO INTEIRA nas extremidades (7ª tarefa, GEOMETRIA VALIDADA VISUALMENTE; CANCELA a 6ª/5ª/4ª; imagens ilegíveis): CORPO horizontal `Alma` `U=[d,L−d]`, `Z∈[0,t]` + `AbaReta` `U=[d,L−d]`, `Z∈[t,aba]` (faixa externa `|V|=meioAlma..meiaLargura`); PERNAS de início `PernaAlma` `U=[d−t,d]` + `PernaAba` `U=[d−aba,d−t]`, `Z=[−d,0]`, e de fim (espelho) `PernaAlma` `U=[L−d,L−d+t]` + `PernaAba` `U=[L−d+t,L−d+aba]` — 90° PARA BAIXO, perfil U preservado, boca da U voltada para o montante, corpo e perna compartilham a linha de dobra (SEM sobreposição de volume). Constante provisória `ProfundidadePernaBlockingMm = 60` (`temPerna = d>0 && L>=2*d`; senão guia cheia). 8ª tarefa: `BlockingCalculator` agora passa `PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `ComprimentoAoLongoEixo=compEntreFaces+2d` (d=60 mm) — a LINHA DE DOBRA cai na face interna do montante e a perna o abraça (corpo exatamente entre as faces); também adiciona `DistribuicaoBlocking {Todos(padrão), Alternado}` + `AlternarInvertido` (filtro por paridade do índice do montante, ordem estável por posição; UI/preferências/aviso/testes). OPÇÃO "Montante adicional adjacente ao existente" **REMOVIDA** (config/cálculo/UI/preferências/avisos/testes) a pedido do usuário — NÃO reintroduzir. Geometria em função pura `BlockingTrackBuilder.GeometriaLocal` (TrechoBlocking: Alma/AbaReta/PernaAlma/PernaAba); material de guia; posição/alturas preservadas. LIMITAÇÃO: canto alma×aba na região dobrada fica vazio (entalhe de alívio não definido); fixação não dimensionada. FEITO: build 0/0 e testes 120/120 no Windows (powershell.exe). PENDENTE: E2E no Revit 2021 (OBRIGATÓRIO) do novo comprimento e da distribuição alternada; engenharia da dobra. Arquitetura: Domain sem Autodesk.Revit.DB. Não modelar fita metálica nem dimensionar a fixação estrutural.

## 23. INSTRUÇÃO PARA O CHATGPT

Leia .continuity/CHATGPT_HANDOFF.md, .continuity/CURRENT_STATE.md, .continuity/CURRENT_HANDOFF.md, ROADMAP.md, DECISOES.md. Confirme que o blocking é uma guia em "U" em que, ao encontrar cada montante, a SEÇÃO INTEIRA (ALMA + AS DUAS ABAS JUNTAS) é cortada e DOBRADA 90° PARA BAIXO, formando uma perna vertical que ABRAÇA o montante, com o corpo horizontal íntegro entre as linhas de dobra — geometria da 7ª tarefa, VALIDADA VISUALMENTE. Nesta 8ª tarefa: (a) a LINHA DE DOBRA cai na FACE INTERNA do montante e a perna o abraça (`PontoInicial=faceA−d`, `PontoFinal=faceB+d`, `Comprimento=compEntreFaces+2d`, d=60 mm provisório), sem lacuna/sobreposição; (b) existe a distribuição ALTERNADA (`DistribuicaoBlocking`, `AlternarInvertido`), com padrão `Todos` preservando o comportamento antigo. E confirme que a opção "Montante adicional adjacente ao existente" foi REMOVIDA. Não declare resolvido sem rodar build/testes no Windows e sem ver no Revit 2021 (versões anteriores reprovaram na captura). Se falhar, olhe `BlockingCalculator` (`PontoInicial/PontoFinal/Comprimento` e filtro de distribuição), `BlockingTrackBuilder.GeometriaLocal` (layout/dobra), os avisos do TaskDialog (ResultadoGeracao.Avisos) e a origem das alturas (BlockingRowsResolver/JuntasPlacasService). Mantenha escopo exclusivo LSF Plugin Revit. Não invente especificação de parafusos nem modele fita metálica sem pedido explícito.

## 24. SINCRONIZAÇÃO COM CURRENT_STATE.md

Regra permanente: sempre que `.continuity/CURRENT_STATE.md` for atualizado por mudança relevante, `.continuity/CHATGPT_HANDOFF.md` e `.continuity/CURRENT_HANDOFF.md` devem ser atualizados imediatamente no mesmo momento, mantendo-se sincronizados. O handoff deve ser autossuficiente para retomada em nova conversa do ChatGPT.