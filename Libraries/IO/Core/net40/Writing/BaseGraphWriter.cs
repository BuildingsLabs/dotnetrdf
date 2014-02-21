using System;
using System.IO;
using VDS.RDF.Graphs;

namespace VDS.RDF.Writing
{
    public abstract class BaseGraphWriter
        : IRdfWriter
    {
        public abstract void Save(IGraph g, TextWriter output);

        public virtual void Save(IGraphStore graphStore, TextWriter output)
        {
            if (graphStore == null) throw new ArgumentNullException("graphStore", "Cannot write RDF from a null graph store");
            if (output == null) throw new ArgumentNullException("output", "Cannot write RDF to a null writer");

            // Grab the default graph (if any) and write it out
            IGraph g = graphStore.HasGraph(Quad.DefaultGraphNode) ? graphStore[Quad.DefaultGraphNode] : new Graph();
            this.Save(g, output);
        }

        /// <summary>
        /// Helper method for generating Parser Warning Events
        /// </summary>
        /// <param name="message">Warning Message</param>
        protected void RaiseWarning(String message)
        {
            if (this.Warning != null)
            {
                this.Warning(message);
            }
        }

        /// <summary>
        /// Event which is raised when there is a non-fatal issue with the RDF being written
        /// </summary>
        public event RdfWriterWarning Warning;
    }
}