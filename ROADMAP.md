# Roadmap - Plugin LSF para Revit

Ultima atualizacao: 05/10/2026

Legenda das fases: [OK] concluida e validada no Revit 2021 | [EM VALIDACAO] feita, aguardando teste no Revit | [EM ANDAMENTO] parcialmente feita | [PENDENTE] nao iniciada.
Os criterios de aceitacao de cada item estao em CHECKLIST.md (TC-001 a TC-057).

## Fase 1 - Infraestrutura [OK]
- [x] Projeto C# (.NET Framework 4.8) criado, referenciando RevitAPI/RevitAPIUI do Revit 2021
- [x] Core/App.cs - aba "LSF Modeler" com os botoes Ativar LSF, Painelizar paredes, Placas e Quantitativo
- [x] Core/Command.cs - ponto de entrada do Ativar LSF, selecao de parede
- [x] Core/RevitVersionAdapter.cs - conversao de unidades centralizada
- [x] LSFRevitPlugin.addin (manifesto) e rodar-testes.ps1

## Fase 2 - Distribuicao de montantes [OK]
- [x] Geometry/WallFraming.cs - distribuicao linear ao longo do eixo, espacamento 40/60 cm
- [x] Geometry/StudSolidBuilder.cs + StudDirectShapeFactory.cs - geometria 3D por DirectShape
- [x] Perfil em C (chapa e labio) para montantes e em U para guias (SecaoPerfil, GuiaBuilder)
- [x] Alinhamento correto nas extremidades da parede
- [x] Aviso quando algum vao entre montantes excede o espacamento definido

## Fase 3 - Calculo estrutural [OK]
- [x] Domain/Structural/Nbr14762Calculator.cs - flambagem local, distorcional, global + flecha L/300
- [x] Testes xUnit do calculo estrutural
- [ ] Coeficientes ainda marcados [VALIDAR] contra o texto oficial da NBR 14762:2010 (TC-056)

## Fase 4 - Encontros entre paredes [OK]
- [x] LJunctionCalculator, TJunctionCalculator e CruzJunctionCalculator - deteccao de L (ponta-ponta), T (ponta-meio) e + (meio-meio)
- [x] Reforco Duplo (L), Triplo (T) e Quadruplo (+), validados visualmente
- [x] Deduplicacao entre paredes (nao duplica ao processar as duas paredes de um encontro)
- [x] Supressao de montantes normais colidindo com reforcos
- [x] Orientacao do C costas com costas nos pares (H e +) e T com montante girado
- [x] Guias nos cantos em L: a parede que chega avanca, a outra recebe o desconto de meia espessura (a confirmar no Revit, TC-019)

## Fase 5 - Vaos (portas e janelas) [OK]
- [x] Deteccao de portas e janelas ja existentes na parede (OpeningReinforcement)
- [x] King e Jack studs, verga e contra-verga
- [x] Supressao de montantes normais dentro do vao
- [x] Cripple studs acima e abaixo do vao (testes unitarios incluidos)
- [x] Guia inferior interrompida nos vaos de porta
- [x] Janela "Ajustar vaos": largura, soleira e topo de cada abertura antes de gerar

## Fase 6 - Interface de geracao [OK]
- [x] Janela de opcoes: sistema (Drywall ou Steel Frame), largura da alma, bitola e espacamento
- [x] Perfis Drywall e Steel Frame (PerfilSpec) aplicados a montantes, guias e reforcos de vao
- [x] Cores por sistema e por funcao (montante, guia, reforco de vao)
- [x] Ultima escolha lembrada entre usos (PreferenciasGeracao)
- [x] Geracao regenera a parede inteira com um unico Ctrl+Z (TransactionGroup)

## Fase 7 - Painelizacao [OK]
- [x] Botao Painelizar paredes: usuario clica os pontos onde cada painel termina
- [x] Redefinir e remover painelizacao; aviso de painelizacao desatualizada
- [x] Geracao da estrutura por painel (montantes de extremidade proprios, grade recomecando, guias cortadas nas divisas)

## Fase 8 - Placas de fechamento [EM VALIDACAO]
Feito e validado:
- [x] Janela Placas: tipo, medida e orientacao por painel e por face, coluna Refazer, Aplicar aos marcados, cadastro de medidas
- [x] Tipos: cimenticia, Glasroc X, OSB e gessos (ST, RU, RF, Performa, Performa RU)
- [x] Juntas sobre montantes e desencontradas; folga do piso de 15 mm
- [x] Recortes de portas e janelas com a medida automatica da esquadria
- [x] Reaproveitamento de sobras no projeto inteiro (da maior peca para a menor)
- [x] Codigos <parede>-P<painel>-F<face>-<nn> e recortes -R<n>
- [x] Junta visivel de 3 mm entre placas na geometria 3D (quantitativo segue nominal)
- [x] Testes unitarios da paginacao: 56 testes passando (todos os testes do projeto)

Aguardando teste no Revit:
- [ ] Largura minima de 40 cm das pecas verticais nas pontas da parede, exceto perto de porta ou janela (TC-032)
- [ ] Juntas afastadas das quinas das aberturas, emendas de preferencia no meio dos vaos (TC-028)
- [ ] Placas no canto em L, no T e no + com a decisao atual de encontros (TC-033, TC-034)
- [ ] Cores por tipo de placa (TC-036)
- [ ] Conferir se sobrou peca menor que o minimo nos testes com todos os paineis marcados

## Fase 9 - Quantitativo e parafusos [EM ANDAMENTO]
- [x] Janela do Quantitativo no Revit, com abas, e CSV com as mesmas tabelas
- [x] Medida dos recortes (aba Recortes e coluna na aba Pecas)
- [x] Parafusos da estrutura: 4 por montante na guia, emendas costas com costas e conexoes entre paineis e entre paredes
- [ ] Parafusos das placas por peca, calculados na geometria (TC-042) - em validacao
- [ ] Parafusos no 3D (TC-043) - aparecem no modelo; ainda precisam de adequacoes (a detalhar)

## Fase 10 - Documentacao e repositorio [EM ANDAMENTO]
- [x] README.md completo (requisitos, compilar, instalar, testes, guia de uso, regras, problemas comuns)
- [ ] CHECKLIST.md, CLAUDE.md e DECISOES.md atualizados (TC-045)
- [ ] .gitignore e controle de versao local, se o projeto passar a usar git
- [ ] Licenca do projeto (hoje sem licenca definida)

## Fase 11 - Proximas funcionalidades (na ordem de prioridade)
1. [ ] Adequacoes dos parafusos no 3D e dos parafusos das placas (Fase 9)
2. [ ] Desenhos (vistas) com placas e recortes etiquetados e cotados, e marca do codigo sobre cada placa visivel em corte (TC-046). Um texto visivel em corte so existe dentro de uma vista (planta, corte ou elevacao)
3. [ ] Duas camadas de placas: juntas desencontradas entre camadas e regras de L, T e + com a espessura somada (TC-047)
4. [ ] Relatorio de rejunte por tipo de placa, indicando junta entre tipos diferentes (TC-048)
5. [ ] Editar parede ja lancada (TC-049)
6. [ ] Furacao dos perfis (TC-050)
7. [ ] Modulo de telhado (TC-051)
8. [ ] Lancar varias paredes seguidas: o botao Ativar LSF continua ativo ate trocar de botao (TC-052)
9. [ ] Corrigir junta de placa coincidindo com abertura vertical de janela e evitar junta vertical repetida no mesmo montante (TC-053)
10. [ ] Codigo de cada elemento obedecendo ao painel a que pertence (hoje aparece so "painel 1") (TC-054)
11. [ ] Selecionar um painel e listar os seus componentes (TC-055)
12. [ ] Validar com o fabricante as regras de fixacao de OSB e Glasroc X (TC-057)

## Fase 12 - Modulos futuros (nao iniciados)
- [ ] Laje (FloorFraming, In-Line Framing)
- [ ] Radier (FoundationRadier)
- [ ] Telhado
- [ ] Diferentes tipos de treliças e formas de montar (BACK TO BACK)
- [ ] Contraventamento em K nas paredes
- [ ] Exportacao DXF/CAM
- [ ] Instalador e assinatura digital (acaba com a pergunta de carregar o plugin a cada compilacao)

## Regras permanentes
- Usar todas as sobras de placa e cortar so quando necessario, para o aproveitamento maximo
- Recortes preferencialmente no meio da parede, longe das extremidades e das aberturas
- Recortes de porta e janela usam a medida automatica da esquadria
- Nada em Domain/ referencia Autodesk.Revit.DB (garante os testes unitarios sem abrir o Revit)