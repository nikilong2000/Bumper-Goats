# 🐐 Bumper Goats

A sumo-style fighting game where goats battle to push each other off a shrinking platform. Features an AI opponent powered by reinforcement learning trained with Unity ML-Agents.

![Unity](https://img.shields.io/badge/Unity-6000.0+-black?logo=unity)
![ML-Agents](https://img.shields.io/badge/ML--Agents-4.0.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 🎮 Game Overview

Two goats face off on a floating platform in an intense battle of positioning and timing. The goal is simple: push your opponent off the edge while avoiding the same fate yourself. As time progresses, the arena shrinks, forcing increasingly aggressive confrontations.

### Features

- **Player vs AI**: Battle against a reinforcement learning-trained AI opponent
- **Dynamic Arena**: Platform shrinks over time, intensifying gameplay
- **Combat Actions**: Charge attacks, dodge, jump, and brace mechanics
- **Stamina System**: Strategic resource management for actions
- **Round-Based Matches**: Best-of-three lives format
- **Visual Effects**: Hit effects and combat feedback

## 🤖 AI System

The AI goat is powered by a neural network trained through **self-play reinforcement learning** using Unity ML-Agents.

### Training Approach

- **Algorithm**: Proximal Policy Optimization (PPO)
- **Self-Play**: AI trains by competing against previous versions of itself
- **ELO Matchmaking**: Balanced opponent selection from policy history
- **Training Steps**: Up to 5,000,000 steps

### Observation Space (25 values)

**Self-Awareness:**
- Position relative to platform center
- Current velocity
- Distance to platform edge
- Facing direction
- State flags (grounded, charging, braced, dodging)
- Stamina level

**Opponent Awareness:**
- Direction to opponent
- Opponent velocity
- Opponent's distance to edge
- Opponent state flags

### Action Space

| Type | Actions |
|------|---------|
| Continuous | Horizontal movement (-1 to +1) |
| Discrete | No Action, Attack, Dodge, Jump, Brace |

## 📁 Project Structure

```
Bumper-Goats/
├── Assets/
│   ├── Scripts/           # Game logic
│   │   ├── AiGoatScript.cs       # ML-Agents AI controller
│   │   ├── GoatController.cs     # Goat movement & combat
│   │   ├── PlayerGoat.cs         # Player input handling
│   │   ├── RoundManager.cs       # Match flow management
│   │   ├── ArenaShrinking.cs     # Platform shrinking logic
│   │   └── FallZoneDetector.cs   # Fall detection
│   ├── Scenes/            # Unity scenes
│   ├── Prefabs/           # Goat & arena prefabs
│   ├── Models/            # Trained ONNX model
│   ├── Materials/         # Visual materials
│   ├── Audio/             # Sound effects & music
│   └── Effects/           # Visual effects
├── Packages/              # Unity package dependencies
├── ProjectSettings/       # Unity project configuration
├── results/               # Training output
│   └── goat_v1/
│       ├── Goat.onnx      # Trained model
│       └── configuration.yaml
└── goat.yaml              # ML-Agents training config
```

## 🛠️ Requirements

- **Unity**: 6000.0+ (Unity 6)
- **Unity ML-Agents**: 4.0.0
- **Python**: 3.10+ (for training)
- **mlagents**: Python package (for training)

## 🚀 Getting Started

### Playing the Game

1. Open the project in Unity 6000.0+
2. Open `Assets/Scenes/MainMenu.unity` or `Assets/fight_scenario_v2.unity`
3. Press Play to start the game

### Controls

| Action | Key |
|--------|-----|
| Move | A/D or Arrow Keys |
| Attack (Charge) | Space |
| Dodge | Shift |
| Jump | W or Up Arrow |
| Brace | S or Down Arrow |

## 🧠 Training the AI

### Prerequisites

Install the ML-Agents Python package:

```bash
pip install mlagents==1.1.0
```

### Running Training

1. Open the training scene in Unity
2. Start training from terminal:

```bash
mlagents-learn goat.yaml --run-id=goat_training
```

3. Press Play in Unity Editor
4. Monitor training with TensorBoard:

```bash
tensorboard --logdir results
```

### Training Configuration

The `goat.yaml` file contains all training hyperparameters:

```yaml
behaviors:
  Goat:
    trainer_type: ppo
    max_steps: 5000000
    
    hyperparameters:
      learning_rate: 3.0e-4
      batch_size: 512
      buffer_size: 5120
      
    network_settings:
      hidden_units: 128
      num_layers: 2
      
    self_play:
      save_steps: 20000
      window: 10
      initial_elo: 1200.0
```

## 📊 Reward System

The AI learns through a carefully designed reward structure:

| Event | Reward |
|-------|--------|
| Victory (opponent falls) | +10.0 |
| Defeat (agent falls) | -10.0 |
| Successful hit | +0.6 |
| Getting hit | -0.5 |
| Successful dodge/jump/brace | +0.1 |
| Failed defensive action | -0.1 |
| Episode timeout (60s) | -0.1 |

Additional shaping rewards encourage:
- Proximity to opponent
- Edge avoidance
- Stamina management
- Effective pushing

## 🎯 Game Mechanics

### Combat

- **Charge Attack**: Powerful forward rush that pushes opponents
- **Dodge**: Quick sidestep to avoid incoming attacks
- **Jump**: Leap over charging opponents
- **Brace**: Reduce knockback from incoming hits

### Stamina

All actions consume stamina. Managing your stamina is crucial for victory:
- Actions become unavailable when stamina is depleted
- Stamina regenerates over time when not acting

### Arena Shrinking

The platform gradually shrinks during each round, forcing confrontation and making positioning increasingly important. Currently disabled.

## 💬 Feedback

We'd love to hear from you! Please take a moment to share your experience playing Bumper Goats:

👉 **[Player Feedback Survey](https://forms.gle/BvoHFue1X7pVNnkc8)**

Your feedback helps us improve the game and prioritize future updates. Thank you!

## 📝 License

This project is licensed under the MIT License.

## 🙏 Acknowledgments

- Unity ML-Agents Team
- Quirky Series Ultimate asset pack
- Handpainted Grass and Ground Textures

