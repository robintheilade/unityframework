using System.Collections.Generic;
using UnityEngine;

namespace RobinTheilade
{
    /// <summary>
    /// A region quadtree implementation used for fast lookup in a two dimensional world.
    /// </summary>
    /// <typeparam name="T">
    /// The type to store inside the tree.
    /// </typeparam>
    /// <remarks>
    /// This implementation is not thread-safe.
    /// </remarks>
    public class Quadtree<T>
    {
        /// <summary>
        /// The maximum number of nodes per region.
        /// </summary>
        public const int REGION_CAPACITY = 4;

        /// <summary>
        /// The nodes inside this region.
        /// </summary>
        private readonly List<QuadtreeNode> nodes = new List<QuadtreeNode>(REGION_CAPACITY);

        /// <summary>
        /// The child trees inside this region.
        /// </summary>
        private Quadtree<T>[] children;

        /// <summary>
        /// The boundaries of this region.
        /// </summary>
        private Rect boundaries;

        /// <summary>
        /// Gets the number of values inside this tree.
        /// </summary>
        public int Count
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Quadtree`1"/> class.
        /// </summary>
        /// <param name="boundaries">
        /// The boundaries of the region.
        /// </param>
        public Quadtree(Rect boundaries)
        {
            this.boundaries = boundaries;
        }

        /// <summary>
        /// Inserts a value into the region.
        /// </summary>
        /// <param name="x">
        /// The X component of the value's position.
        /// </param>
        /// <param name="y">
        /// The y component of the value's position.
        /// </param>
        /// <param name="value">
        /// The value to insert.
        /// </param>
        /// <returns>
        /// true if the value was inserted into the region;
        /// false if the value's position was outside the region.
        /// </returns>
        public bool Insert(float x, float y, T value)
        {
            var position = new Vector2(x, y);
            return this.Insert(position, value);
        }

        /// <summary>
        /// Inserts a value into the region.
        /// </summary>
        /// <param name="position">
        /// The position of the value.
        /// </param>
        /// <param name="value">
        /// The value to insert.
        /// </param>
        /// <returns>
        /// true if the value was inserted into the region;
        /// false if the value's position was outside the region.
        /// </returns>
        public bool Insert(Vector2 position, T value)
        {
            var node = new QuadtreeNode(position, value);
            return this.Insert(node);
        }

        /// <summary>
        /// Inserts a node into the region.
        /// </summary>
        /// <param name="node">
        /// The node to insert.
        /// </param>
        /// <returns>
        /// true if the node was inserted into the region;
        /// false if the position of the node was outside the region.
        /// </returns>
        private bool Insert(QuadtreeNode node)
        {
            if (!this.boundaries.Contains(node.Position))
            {
                return false;
            }

            if (this.children != null)
            {
                for (var index = 0; index < this.children.Length; index++)
                {
                    var child = this.children[index];
                    if (child.Insert(node))
                    {
                        this.Count++;
                        return true;
                    }
                }
            }

            if (this.nodes.Count < REGION_CAPACITY)
            {
                this.nodes.Add(node);
                this.Count++;
                return true;
            }

            this.Subdivide();
            return this.Insert(node);
        }

        /// <summary>
        /// Returns the values that are within the specified <paramref name="range"/>.
        /// </summary>
        /// <param name="range">
        /// A rectangle representing the region to query.
        /// </param>
        /// <returns>
        /// Any value found inside the specified <paramref name="range"/>.
        /// </returns>
        public IEnumerable<T> Find(Rect range)
        {
            if (this.Count == 0)
            {
                yield break;
            }

            var allowInverse = false;
            if (!this.boundaries.Overlaps(range, allowInverse))
            {
                yield break;
            }

            if (this.children == null)
            {
                for (var index = 0; index < this.nodes.Count; index++)
                {
                    var node = this.nodes[index];
                    if (range.Contains(node.Position))
                    {
                        yield return node.Value;
                    }
                }
            }
            else
            {
                for (var index = 0; index < this.children.Length; index++)
                {
                    var child = this.children[index];

                    foreach (var value in child.Find(range))
                    {
                        yield return value;
                    }
                }
            }
        }

        /// <summary>
        /// Removes a value from the region.
        /// </summary>
        /// <param name="x">
        /// The X component of the value's position.
        /// </param>
        /// <param name="y">
        /// The y component of the value's position.
        /// </param>
        /// <param name="value">
        /// The value to remove.
        /// </param>
        /// <returns>
        /// true if the value was removed from the region;
        /// false if the value's position was outside the region.
        /// </returns>
        public bool Remove(float x, float z, T value)
        {
            return this.Remove(new Vector2(x, z), value);
        }

        /// <summary>
        /// Removes a value from the region.
        /// </summary>
        /// <param name="position">
        /// The position of the value.
        /// </param>
        /// <param name="value">
        /// The value to remove.
        /// </param>
        /// <returns>
        /// true if the value was removed from the region;
        /// false if the value's position was outside the region.
        /// </returns>
        public bool Remove(Vector2 position, T value)
        {
            if (this.Count == 0)
            {
                return false;
            }

            if (!this.boundaries.Contains(position))
            {
                return false;
            }

            if (this.children != null)
            {
                var isRemoved = false;

                for (var index = 0; index < this.children.Length; index++)
                {
                    var child = this.children[index];
                    if (!isRemoved && child.Remove(position, value))
                    {
                        isRemoved = true;
                        this.Count--;
                        break;
                    }
                }

                if (this.Count <= REGION_CAPACITY)
                {
                    this.Combine();
                }

                return isRemoved;
            }

            for (var index = 0; index < this.nodes.Count; index++)
            {
                var node = this.nodes[index];
                if (node.Position.Equals(position))
                {
                    this.nodes.RemoveAt(index);
                    this.Count--;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Splits the region into 4 new subregions and moves the existing values into the new subregions.
        /// </summary>
        private void Subdivide()
        {
            this.children = new Quadtree<T>[4];

            var width = this.boundaries.width * 0.5f;
            var height = this.boundaries.height * 0.5f;

            for (var index = 0; index < this.children.Length; index++)
            {
                var boundaries = new Rect(
                    this.boundaries.xMin + width * (index % 2),
                    this.boundaries.yMin + height * (index / 2),
                    width,
                    height
                    );

                this.children[index] = new Quadtree<T>(boundaries);
            }

            this.Count = 0;

            for (var index = 0; index < this.nodes.Count; index++)
            {
                var node = this.nodes[index];
                this.Insert(node);
            }

            this.nodes.Clear();
        }

        /// <summary>
        /// Joins the contents of the children into this region and remove the child regions.
        /// </summary>
        private void Combine()
        {
            for (var index = 0; index < this.children.Length; index++)
            {
                var child = this.children[index];
                this.nodes.AddRange(child.nodes);
            }

            this.children = null;
        }

        /// <summary>
        /// A single node inside a quadtree used for keeping values and their position.
        /// </summary>
        private class QuadtreeNode
        {
            /// <summary>
            /// Gets the position of the value.
            /// </summary>
            public Vector2 Position
            {
                get;
                private set;
            }

            /// <summary>
            /// Gets the value.
            /// </summary>
            public T Value
            {
                get;
                private set;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:QuadtreeNode"/> class.
            /// </summary>
            /// <param name="position">
            /// The position of the value.
            /// </param>
            /// <param name="value">
            /// The value.
            /// </param>
            public QuadtreeNode(Vector2 position, T value)
            {
                this.Position = position;
                this.Value = value;
            }
        }
    }
}
