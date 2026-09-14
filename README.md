\# Procedural Dungeon Generation in Roguelike Games



CM3070 Final Project  

University of London



\## Overview



This project is a 2D roguelike prototype developed in Unity to investigate

procedural dungeon generation and its integration with gameplay systems.



The dungeon generator combines Binary Space Partitioning (BSP) with

cellular-automata-inspired room shaping. Generated dungeon structure is then

used by semantic room assignment, procedural content placement, enemy AI,

objectives, environmental decoration and runtime terrain modification.



\## Main Features



\- Deterministic seed-based dungeon generation

\- Binary Space Partitioning room generation

\- Cellular-automata-inspired room shaping

\- Graph-based corridor generation and validation

\- Semantic room roles

\- Procedural enemies, items and environmental decoration

\- Directional enemy perception and line of sight

\- Persistent Warden enemy using weighted A\* pathfinding

\- Runtime terrain modification using the Digger/Shaper mechanic

\- Limited Pulse Charges for enemy crowd control

\- Multi-floor Standard and Survival modes

\- Adaptive difficulty

\- Procedural visual themes

\- Fog of war and directional player vision

\- Resonance puzzle rooms

\- Automated dungeon evaluation

\- WebGL playtest telemetry



\## Controls



\- WASD / Arrow Keys - Move

\- Q - Deploy Pulse Charge

\- F - Use Digger

\- E - Interact

\- Tab - Dungeon overview



Additional contextual instructions are displayed during gameplay.



\## Unity Version



Unity 2019.4.41f2



\## Running the Project



1\. Clone or download the repository.

2\. Open the project using Unity 2019.4.41f2.

3\. Open `Assets/Scenes/DungeonGeneration.unity`.

4\. Enter Play Mode.



The project was also tested as a WebGL build for external playtesting.



The WebGL can be accessed her: https://bacanuionut.itch.io/procedural-dungeon-generator-cm3070-playtest



\## Procedural Generation



The main generation pipeline consists of:



1\. BSP partitioning

2\. Room generation

3\. Dungeon graph construction

4\. Corridor generation

5\. Unified grid construction

6\. Cellular-automata-inspired room shaping

7\. Validation

8\. Semantic room assignment

9\. Procedural gameplay and environmental content

10\. Rendering and gameplay initialisation



Using the same seed reproduces the same generated dungeon structure.



\## Evaluation



Automated evaluation tools are included under:



`Assets/Scripts/Evaluation/`



Final generation datasets are stored under:



`Assets/EvaluationResults/`



The final generator was evaluated across 1,000 deterministic seeds using

structural, gameplay-path and generation-performance metrics.



\## Project Structure



`Assets/Scripts/Generation/`  

Core procedural generation, graph, grid and room systems.



`Assets/Scripts/Gameplay/`  

Player systems, enemy AI, Warden behaviour, rendering and gameplay mechanics.



`Assets/Scripts/Evaluation/`  

Dungeon validation, metrics and automated batch evaluation.



`Assets/Scripts/Telemetry/`  

Anonymous WebGL playtest telemetry.



`Assets/EvaluationResults/`  

Generated evaluation datasets.



\## Third-Party Assets



The project uses selected third-party graphical and audio assets. Full asset

sources and acknowledgements are provided in the accompanying final project

report.



All third-party assets remain subject to the terms specified by their

respective creators.



\## Author



Ionut-Alexandru Bacanu

