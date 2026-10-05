# Registro de Decisoes - Plugin LSF para Revit

## [ADR-001] Injecao de estrutura em parede composta parametrica
Manter a parede arquitetonica ativa no Revit como "Parede Composta" (camadas de acabamento) e usar o plugin so pra preencher o vazio interno com a estrutura metalica. Preserva cotas, tags e links entre disciplinas.

## [ADR-004] Elementos estruturais como DirectShape, nao FamilyInstance
Montantes gerados como geometria leve (DirectShape com solido extrudado), nao FamilyInstance em massa. Um edificio termico comum gera 1.500-3.000 elementos; FamilyInstance em massa degrada performance (regeneracao, selecao, navegacao 3D).

## [ADR-005] Separacao entre calculo puro (Domain/) e API do Revit (Geometry/)
Nada em Domain/ referencia Autodesk.Revit.DB. Permite testes unitarios automatizados (xUnit) sem abrir o Revit -- essencial pro modulo estrutural (Nbr14762Calculator).

## [ADR-006] RevitVersionAdapter isola conversao de unidades
Todo ponto que historicamente muda entre versoes do Revit (unidades, ForgeTypeId) passa por Core/RevitVersionAdapter.cs, nunca chamado direto nos modulos de negocio. Alvo real hoje: Revit 2021.

## [ADR-007] Nomenclatura de encontros baseada no blog da Barbieri
Duplo = encontro em L, Triplo = encontro em T, Quadruplo = encontro em +. Terminologia de mercado (light steel frame), nao inventada pelo projeto.

## [ADR-008] Formulas finais calibradas para os 3 tipos de encontro (validadas visualmente)
- L (Duplo): isolado da parede + par H (2 montantes costas-com-costas), offset = espessuraNucleoOutraParede/2 -+ larguraPerfil/2. Isolado usa sinal negativo se cria o H, positivo se o H ja existe (criado pela outra parede).
- T (Triplo): parede que chega gera 1 montante rotacionado 90 graus, deslocado 6,5cm do PontoT na direcao dela mesma (nao gera isolado normal nesse ponto). Parede passante, processada separadamente (deteccao espelhada DetectarEncontrosEmTComoPassante), gera 3 montantes proprios: 1 rotacionado a 2,5cm na direcao da parede que chega + 2 normais a 6,5cm cada lado.
- + (Quadruplo): 1a parede processada gera 4 montantes (2 pares Duplo espelhados pros dois lados, offset = espessuraNucleoCruzada/2 -+ larguraPerfil/2). 2a parede (cruzada), ao detectar que o Quadruplo ja existe, gera so 2 montantes proprios a 6,5cm cada lado.
- Espessuras sempre lidas do NUCLEO estrutural real (WallType.GetCompoundStructure, Function=Structure), nunca a espessura total da parede.
- Deduplicacao por nome unico gravado no DirectShape, codificando a coordenada do encontro.

## [ADR-009] Divisao de WallJunctions.cs em 3 calculadoras + 1 helper
WallJunctions.cs cresceu demais (L+T+cruz juntos). Dividido em Geometry/WallGeometryHelpers.cs (funcoes compartilhadas), LJunctionCalculator.cs, TJunctionCalculator.cs, CruzJunctionCalculator.cs -- todos estaticos, sem estado.

## [ADR-010] Nomenclatura de verga/contra-verga em vaos (invertida do termo comum)
Por decisao do usuario, neste projeto: "verga" = elemento horizontal na altura da soleira (janela, embaixo), "contra-verga" = elemento horizontal no topo do vao (porta e janela, sustenta carga de cima). Inverso do uso mais comum na construcao civil, mas e a convencao adotada aqui -- manter consistente em todo o codigo e documentacao.

## [ADR-011] Reforco de vao: King Studs + Jack Studs + verga/contra-verga
2 King Studs (altura total) nas bordas externas do vao + 2 Jack Studs (piso ate o topo do vao) encostados neles por dentro + elemento horizontal "contra-verga" no topo do vao + elemento horizontal "verga" na soleira (so janela). Montantes normais dentro da faixa do vao sao suprimidos.
