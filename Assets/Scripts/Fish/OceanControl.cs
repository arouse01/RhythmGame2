using UnityEngine;

// https://discussions.unity.com/t/looping-background-scroll-animation/360271/2

public class OceanControl : MonoBehaviour
{
    [SerializeField] private Renderer rend;
    [SerializeField] private float Speed = 1f;

    private float spriteWidth;
    private float originX;

    bool IsPaused = true;

    void Start()
    {
        originX = transform.position.x;

        //This code gets the width of the sprite in world units.
        //I'm assuming that there is no scaling done in the Transform.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Bounds spriteBounds = sr.sprite.bounds;
        spriteWidth = spriteBounds.size.x;
    }

    void Update()
    {
        if (!IsPaused)
        {
            //Every frame, move the image too the left based on the speed and elapsed time
            transform.Translate(-Speed * Time.deltaTime, 0, 0);

            //Once we have moved one full sprite width, jump back exactly one width.
            //Since this is a tiled image, the pixels after the jump will be exactly
            // the same as before the jump, so this jump is not noticable.
            if (transform.position.x < (originX - spriteWidth))
            {
                transform.Translate(spriteWidth, 0, 0);
            }
        }
    }
    public void SetOceanSpeed(float speed)
    {
        Speed = speed;
    }

    public void PauseWaves(bool pause)
    {
        IsPaused = pause;
    }
}
