# WolfHub — Visão e intenções do projeto

## Propósito

WolfHub é um toolkit pessoal, modular e open-source para preparar, configurar e manter computadores Windows. Ele reúne em um só lugar instaladores, ferramentas portáteis, perfis e ajustes que hoje ficam espalhados entre scripts, arquivos e procedimentos manuais.

O objetivo é reduzir o trabalho depois de formatar um computador e tornar o suporte aos computadores de amigos mais repetível, compreensível e seguro.

## Para quem

- Para quem quer preparar um Windows novo sem repetir uma lista de tarefas manualmente.
- Para entusiastas e técnicos que mantêm um conjunto próprio de ferramentas.
- Para quem presta suporte e precisa explicar o que uma ação fará antes de executá-la.

WolfHub começa como um projeto pessoal, mas deve evitar depender do hardware, dos caminhos ou das preferências de uma única máquina. Arquivos pessoais e pacotes de drivers específicos permanecem fora do catálogo público até serem generalizados e validados.

## Princípios de produto

1. **Ação clara:** cada ação informa o que altera, suas dependências, as permissões esperadas e o limite de reversão.
2. **Confirmação antes de mudanças:** instalações, ajustes e ferramentas externas exigem uma confirmação compreensível.
3. **Repetibilidade:** detectar o estado atual, evitar trabalho desnecessário e manter operações previsíveis.
4. **Modularidade:** o hub coordena módulos com propósito e ciclo de vida próprios; não precisa possuir a lógica interna de cada aplicativo.
5. **Portabilidade:** configurações e perfis não devem embutir caminhos absolutos de usuário nem presumir hardware idêntico.
6. **Dados locais:** preferências e backups ficam no perfil do usuário, sem envio automático para serviços externos.
7. **Segurança gradual:** executar sem administrador por padrão; pedir elevação somente quando a ação escolhida realmente precisar dela.
8. **Transparência sobre reversão:** um inventário ou backup parcial não deve ser apresentado como restauração completa do Windows.

## Como imaginamos o uso

O usuário abre o WolfHub, escolhe um módulo ou perfil, revisa a lista de ações e confirma. O aplicativo mostra o resultado de cada etapa e preserva informação suficiente para comparar o estado posterior com o inicial, dentro do que cada módulo consegue suportar.

Perfis são conjuntos reutilizáveis de referências a um catálogo central. Eles descrevem uma configuração desejada; não são scripts independentes nem contêm comandos arbitrários.

## Organização pretendida

- **Hub central:** navegação, perfis de preparação, descoberta de módulos e visão de estado.
- **Catálogos declarativos:** módulos em `config/modules.json`, ações em `config/features.json` e pacotes em `config/packages.json`.
- **Perfis:** arquivos JSON em `profiles/` que referenciam IDs do catálogo.
- **Módulos:** executáveis e componentes independentes, com requisitos, armazenamento e atualização próprios.
- **Dados locais:** configurações e cópias de segurança em `%LOCALAPPDATA%`.
- **Documentação e verificações:** explicam contratos e limitações sem executar mudanças reais durante testes.

## Backup e restauração

Queremos um ponto de partida local para ajudar o usuário a entender e reverter ajustes que o WolfHub consegue restaurar com fidelidade. Hoje o backup inicial cobre somente o plano de energia ativo, os valores do Explorador alterados pelo hub, a chave do menu de contexto gerida pelo módulo e uma lista limitada de aplicativos conhecidos.

Essa cópia não é imagem do sistema, ponto de restauração do Windows, backup de arquivos pessoais ou mecanismo universal de downgrade. Instalações de software e drivers podem exigir os desinstaladores ou mecanismos de recuperação do respectivo fornecedor.

## WolfHub Audio

O WolfHub Audio é um aplicativo Windows independente, integrado ao ecossistema WolfHub como módulo. Sua finalidade é gerenciar perfis de saída e gravação, testar endpoints, exibir o nível do microfone e alternar saídas pela bandeja.

O Audio Switcher deve conservar seu próprio executável, armazenamento em `%LOCALAPPDATA%\WolfHubAudio\settings.xml` e processo de atualização. O hub pode apresentá-lo, abri-lo ou instalar/atualizar seu binário sem apagar suas preferências.

## Escopo e evolução

### Prioridade atual

- Tornar os fluxos existentes consistentes e compreensíveis.
- Validar catálogo, perfis, dependências e comportamento sem instalar programas de verdade.
- Melhorar o backup limitado e a indicação do estado de cada ação.
- Manter o Audio Switcher funcional como aplicativo independente.

### Depois

- Tweaks com simulação, detecção de estado e reversão claramente descrita.
- Debloat seletivo que preserve componentes essenciais e ofereça uma explicação por item.
- Ferramentas portáteis e downloads de fontes oficiais com verificação de integridade.
- Consulta e instalação assistida de drivers com identificação exata do fabricante e modelo.
- Atualização e distribuição versionadas, com validação de integridade antes de executar conteúdo baixado.
- Exportar e importar perfis de configuração sem caminhos ou identificadores da máquina de origem.

## O que o projeto não pretende fazer

- Executar scripts remotos sem validação.
- Instalar ou alterar o Windows sem uma escolha explícita do usuário.
- Desabilitar Defender, Firewall ou Windows Update.
- Incluir cracks, ativadores ou bypass de licenças.
- Instalar drivers com base apenas em nomes parecidos.
- Fazer de um backup parcial uma promessa de recuperação completa.
- Tornar o aplicativo dependente de um serviço Windows, banco de dados ou framework desktop pesado.

## Referências de produto

Projetos como WinUtil, UniGetUI e Sophia Script são referências para estudar organização por tarefas, seleção de pacotes, presets e descrição de ajustes. WolfHub deve aproveitar padrões úteis à sua escala e ao seu propósito; manter identidade, código e decisões próprias.
