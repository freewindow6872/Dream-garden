#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BotAI))]
[CanEditMultipleObjects]
public class BotAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty typeProp = serializedObject.FindProperty("type");
        EditorGUILayout.PropertyField(typeProp, new GUIContent("봇 타입 (6종 통합)"));

        BotAI.BotType botType = (BotAI.BotType)typeProp.enumValueIndex;

        EditorGUILayout.Space(4);
        DrawProps("player", "maxHealth", "gearReward", "alertColor");

        if (botType == BotAI.BotType.Drone)
        {
            DrawProps(
                "droneState", "droneSpriteFacesLeft", "droneDetectionRadius", "droneLoseAggroRadius",
                "droneFreePatrolSpeed", "droneFreePatrolRadius", "droneAggroSpeedMultiplier",
                "droneTargetOffsetY", "recognitionDelay", "wallCheckDistance",
                "droneContactDamage", "droneContactKnockback", "droneContactCooldown",
                "droneContactOnlyInAggro", "droneHitStunDuration", "droneHitKnockbackScale"
            );
        }
        else
        {
            DrawProps(
                "patrolSpeed", "chaseSpeedMultiplier", "usePatrolBounds", "patrolMinX", "patrolMaxX",
                "wallCheckDistance", "groundCheckDistance", "verticalDetectionTolerance", "recognitionDelay"
            );

            switch (botType)
            {
                case BotAI.BotType.Scout:
                    DrawProps("scoutFrontRange", "scoutBackRange", "meleeDamage", "meleeRange", "meleeVerticalRange", "meleeCooldown", "meleeKnockback");
                    break;

                case BotAI.BotType.Sniper:
                    DrawProps(
                        "sniperRange", "rangedPreferredDistance", "rangedDistanceTolerance",
                        "rangedRepositionSpeedMultiplier", "sniperDamage", "sniperLaserLength",
                        "laserCooldown", "laserTelegraphTime", "laserVerticalTolerance", "laserWidth", "sniperKnockback"
                    );
                    break;

                case BotAI.BotType.ChargeBot:
                    DrawProps("scoutFrontRange", "scoutBackRange", "chargeSpeedMultiplier", "recoverySeconds", "mixerAttackRange", "mixerAttackVerticalRange");
                    break;

                case BotAI.BotType.MixerBot:
                case BotAI.BotType.MixerChargeBot:
                    DrawProps(
                        "scoutFrontRange", "scoutBackRange", "chargeSpeedMultiplier", "recoverySeconds",
                        "mixerAttackRange", "mixerAttackVerticalRange", "mixerAttackDamage",
                        "spinHitCount", "spinHitInterval", "spinDuration", "mixerAttackCooldown",
                        "mixerKnockback", "showMixerAttackVisual"
                    );
                    break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProps(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty prop = serializedObject.FindProperty(names[i]);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, true);
        }
    }
}
#endif