using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GardenerEditor
{
    /// <summary>씬의 PlayerController2D 설정으로 계산한 점프 수치 (맵 디자인 참고용 근사치)</summary>
    public struct JumpStats
    {
        public bool valid;

        public float moveSpeed;
        public float jumpForce;
        public float gravityUp;
        public float gravityDown;
        public float airJumpForce;
        public float dashSpeed;
        public float dashDuration;

        public float jumpHeight;        // 점프 최대 높이 (점프 키를 누르고 있을 때)
        public float jumpRange;         // 출발 높이로 되돌아올 때까지의 수평 거리
        public float doubleHeight;      // 이단 점프 포함 최대 높이
        public float doubleRange;
        public float dashJumpRange;     // 점프 + 공중 대시
        public float groundDashDistance;
    }

    public static class MapEditorJump
    {
        private const float Dt = 0.02f; // 기본 FixedUpdate 간격

        private static float Prop(SerializedObject so, string propName, float fallback)
        {
            SerializedProperty p = so.FindProperty(propName);
            return p != null ? p.floatValue : fallback;
        }

        public static JumpStats Read()
        {
            JumpStats s = new JumpStats();

            PlayerController2D pc = Object.FindObjectOfType<PlayerController2D>();
            if (pc == null) return s;

            SerializedObject so = new SerializedObject(pc);
            float fallMult = Prop(so, "fallGravityMultiplier", 2.2f);
            float airMult = Prop(so, "airJumpForceMultiplier", 0.9f);

            s.moveSpeed = Prop(so, "moveSpeed", 7f);
            s.jumpForce = Prop(so, "jumpForce", 13f);
            s.airJumpForce = s.jumpForce * airMult;
            s.dashSpeed = Prop(so, "dashSpeed", 20f);
            s.dashDuration = Prop(so, "dashDuration", 0.16f);

            // PlayerController2D 의 중력 처리 방식 그대로:
            //  - 상승/기본: Physics2D.gravity * gravityScale
            //  - 하강: 위 값 + Physics2D.gravity * (fallGravityMultiplier - 1) / mass  (AddForce 이므로 gravityScale 미적용)
            Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
            float g = Mathf.Abs(Physics2D.gravity.y);
            float scale = rb != null ? rb.gravityScale : 1f;
            float mass = rb != null ? Mathf.Max(0.0001f, rb.mass) : 1f;
            s.gravityUp = g * scale;
            s.gravityDown = s.gravityUp + g * Mathf.Max(0f, fallMult - 1f) / mass;

            float apex;
            float range;

            Measure(Simulate(s, Vector2.zero, 1, false, false, 3f), out apex, out range);
            s.jumpHeight = apex;
            s.jumpRange = range;

            Measure(Simulate(s, Vector2.zero, 1, true, false, 3f), out apex, out range);
            s.doubleHeight = apex;
            s.doubleRange = range;

            Measure(Simulate(s, Vector2.zero, 1, false, true, 3f), out apex, out range);
            s.dashJumpRange = range;

            s.groundDashDistance = s.dashSpeed * s.dashDuration;
            s.valid = true;
            return s;
        }

        /// <summary>
        /// 점프 궤적 (플레이어 중심 좌표). start 에서 점프 키를 누른 채로 출발.
        /// airJump: 정점에서 이단 점프 / dash: 정점에서 공중 대시
        /// </summary>
        public static List<Vector3> Simulate(JumpStats s, Vector2 start, int dir, bool airJump, bool dash, float dropBelow)
        {
            List<Vector3> pts = new List<Vector3>();
            Vector2 p = start;
            float vx = dir * s.moveSpeed;
            float vy = s.jumpForce;
            bool usedExtra = false;
            float dashLeft = 0f;

            pts.Add(new Vector3(p.x, p.y, 0f));

            for (int i = 0; i < 600; i++)
            {
                bool dashing = dashLeft > 0f;

                // 정점(상승이 끝나는 순간)에서 추가 동작
                if (!dashing && !usedExtra && vy <= 0f)
                {
                    if (airJump)
                    {
                        usedExtra = true;
                        vy = s.airJumpForce;
                    }
                    else if (dash)
                    {
                        usedExtra = true;
                        dashLeft = s.dashDuration;
                        dashing = true;
                    }
                }

                if (dashing)
                {
                    vx = dir * s.dashSpeed;
                    vy = 0f;
                    dashLeft -= Dt;
                    if (dashLeft <= 0f)
                    {
                        vx = dir * s.moveSpeed;
                        vy = 0f;
                    }
                }
                else
                {
                    vy -= (vy > 0f ? s.gravityUp : s.gravityDown) * Dt;
                }

                p += new Vector2(vx, vy) * Dt;
                pts.Add(new Vector3(p.x, p.y, 0f));

                if (p.y < start.y - dropBelow) break;
            }

            return pts;
        }

        /// <summary>최대 높이와, 출발 높이(y=0)로 내려오는 지점까지의 수평 거리</summary>
        private static void Measure(List<Vector3> pts, out float apex, out float range)
        {
            apex = 0f;
            range = 0f;

            for (int i = 0; i < pts.Count; i++)
                apex = Mathf.Max(apex, pts[i].y);

            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i - 1].y > 0f && pts[i].y <= 0f)
                {
                    float t = pts[i - 1].y / (pts[i - 1].y - pts[i].y);
                    range = Mathf.Abs(Mathf.Lerp(pts[i - 1].x, pts[i].x, t));
                }
            }
        }
    }
}
