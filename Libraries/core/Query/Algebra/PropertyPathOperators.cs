﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDS.RDF.Query.Paths;
using VDS.RDF.Query.Patterns;

namespace VDS.RDF.Query.Algebra
{
    public interface IPathOperator : ISparqlAlgebra
    {
        PatternItem PathStart
        {
            get;
        }

        PatternItem PathEnd
        {
            get;
        }

        ISparqlPath Path
        {
            get;
        }
    }

    public abstract class BasePathOperator : IPathOperator
    {
        private PatternItem _start, _end;
        private ISparqlPath _path;
        private HashSet<String> _vars = new HashSet<string>();

        public BasePathOperator(PatternItem start, PatternItem end, ISparqlPath path)
        {
            this._start = start;
            this._end = end;
            this._path = path;

            if (this._start.VariableName != null) this._vars.Add(this._start.VariableName);
            if (this._end.VariableName != null) this._vars.Add(this._end.VariableName);
        }

        public PatternItem PathStart
        {
            get 
            { 
                return this._start; 
            }
        }

        public PatternItem PathEnd
        {
            get 
            { 
                return this._end; 
            }
        }

        public ISparqlPath Path
        {
            get 
            { 
                return this._path;
            }
        }

        public abstract BaseMultiset Evaluate(SparqlEvaluationContext context);

        public IEnumerable<string> Variables
        {
            get 
            {
                return this._vars;
            }
        }

        public SparqlQuery ToQuery()
        {
            SparqlQuery q = new SparqlQuery();
            q.RootGraphPattern = this.ToGraphPattern();
            q.Optimise();
            return q;
        }

        public abstract GraphPattern ToGraphPattern();

        public abstract override string ToString();
    }

    public class ZeroLengthPath : BasePathOperator
    {
        public ZeroLengthPath(PatternItem start, PatternItem end, ISparqlPath path)
            : base(start, end, path) { }
    
        public override BaseMultiset Evaluate(SparqlEvaluationContext context)
        {
            if (this.AreBothTerms())
            {
                if (this.AreSameTerms())
                {
                    return new IdentityMultiset();
                }
                else
                {
                    return new NullMultiset();
                }
            }

            String subjVar = this.PathStart.VariableName;
            String objVar = this.PathEnd.VariableName;

            //Determine the Triples to which this applies
            IEnumerable<Triple> ts = null;
            if (subjVar != null)
            {
                //Subject is a Variable
                if (context.InputMultiset.ContainsVariable(subjVar))
                {
                    //Subject is Bound
                    if (objVar != null)
                    {
                        //Object is a Variable
                        if (context.InputMultiset.ContainsVariable(objVar))
                        {
                            //Object is Bound
                            ts = (from s in context.InputMultiset.Sets
                                  where s[subjVar] != null && s[objVar] != null
                                  from t in context.Data.GetTriplesWithSubjectObject(s[subjVar], s[objVar])
                                  select t);
                        }
                        else
                        {
                            //Object is Unbound
                            ts = (from s in context.InputMultiset.Sets
                                  where s[subjVar] != null
                                  from t in context.Data.GetTriplesWithSubject(s[subjVar])
                                  select t);
                        }
                    }
                    else
                    {
                        //Object is a Term
                        //Preseve sets where the Object Term is equal to the currently bound Subject
                        INode objTerm = ((NodeMatchPattern)this.PathEnd).Node;
                        foreach (Set s in context.InputMultiset.Sets)
                        {
                            INode temp = s[subjVar];
                            if (temp != null && temp.Equals(objTerm))
                            {
                                context.OutputMultiset.Add(new Set(s));
                            }
                        }
                    }
                }
                else
                {
                    //Subject is Unbound
                    if (objVar != null)
                    {
                        //Object is a Variable
                        if (context.InputMultiset.ContainsVariable(objVar))
                        {
                            //Object is Bound
                            ts = (from s in context.InputMultiset.Sets
                                  where s[objVar] != null
                                  from t in context.Data.GetTriplesWithObject(s[objVar])
                                  select t);
                        }
                        else
                        {
                            //Object is Unbound
                            HashSet<INode> nodes = new HashSet<INode>();
                            foreach (Triple t in context.Data.Triples)
                            {
                                nodes.Add(t.Subject);
                                nodes.Add(t.Object);
                            }
                            foreach (INode n in nodes)
                            {
                                Set s = new Set();
                                s.Add(subjVar, n);
                                s.Add(objVar, n);
                                context.OutputMultiset.Add(s);
                            }
                        }
                    }
                    else
                    {
                        //Object is a Term
                        //Create a single set with the Variable bound to the Object Term
                        Set s = new Set();
                        s.Add(subjVar, ((NodeMatchPattern)this.PathEnd).Node);
                        context.OutputMultiset.Add(s);
                    }
                }
            }
            else if (objVar != null)
            {
                //Subject is a Term but Object is a Variable
                if (context.InputMultiset.ContainsVariable(objVar))
                {
                    //Object is Bound
                    //Preseve sets where the Subject Term is equal to the currently bound Object
                    INode subjTerm = ((NodeMatchPattern)this.PathStart).Node;
                    foreach (Set s in context.InputMultiset.Sets)
                    {
                        INode temp = s[objVar];
                        if (temp != null && temp.Equals(subjTerm))
                        {
                            context.OutputMultiset.Add(new Set(s));
                        }
                    }
                }
                else
                {
                    //Object is Unbound
                    //Create a single set with the Variable bound to the Suject Term
                    Set s = new Set();
                    s.Add(objVar, ((NodeMatchPattern)this.PathStart).Node);
                    context.OutputMultiset.Add(s);
                }
            }
            else
            {
                //Should already have dealt with this earlier (the AreBothTerms() and AreSameTerms() branch)
                throw new RdfQueryException("Reached unexpected point of ZeroLengthPath evaluation");
            }

            //Get the Matches only if we haven't already generated the output
            if (ts != null)
            {
                HashSet<KeyValuePair<INode, INode>> matches = new HashSet<KeyValuePair<INode, INode>>();
                foreach (Triple t in ts)
                {
                    if (this.PathStart.Accepts(context, t.Subject) && this.PathEnd.Accepts(context, t.Object))
                    {
                        matches.Add(new KeyValuePair<INode, INode>(t.Subject, t.Object));
                    }
                }

                //Generate the Output based on the mathces
                if (matches.Count == 0)
                {
                    context.OutputMultiset = new NullMultiset();
                }
                else
                {
                    if (this.PathStart.VariableName == null && this.PathEnd.VariableName == null)
                    {
                        context.OutputMultiset = new IdentityMultiset();
                    }
                    else
                    {
                        context.OutputMultiset = new Multiset();
                        foreach (KeyValuePair<INode, INode> m in matches)
                        {
                            Set s = new Set();
                            if (subjVar != null) s.Add(subjVar, m.Key);
                            if (objVar != null) s.Add(objVar, m.Value);
                            context.OutputMultiset.Add(s);
                        }
                    }
                }
            }
            return context.OutputMultiset;
        }

        private bool AreBothTerms()
        {
            return (this.PathStart.VariableName == null && this.PathEnd.VariableName == null);
        }

        private bool AreSameTerms()
        {
            if (this.PathStart is NodeMatchPattern && this.PathEnd is NodeMatchPattern)
            {
                return ((NodeMatchPattern)this.PathStart).Node.Equals(((NodeMatchPattern)this.PathEnd).Node);
            }
            else if (this.PathStart is FixedBlankNodePattern && this.PathEnd is FixedBlankNodePattern)
            {
                return ((FixedBlankNodePattern)this.PathStart).InternalID.Equals(((FixedBlankNodePattern)this.PathEnd).InternalID);
            }
            else
            {
                return false;
            }
        }

        public override string  ToString()
        {
            return "ZeroLengthPath(" + this.PathStart.ToString() + ", " + this.Path.ToString() + ", " + this.PathEnd.ToString() + ")";
        }

        public override GraphPattern ToGraphPattern()
        {
            GraphPattern gp = new GraphPattern();
            PropertyPathPattern pp = new PropertyPathPattern(this.PathStart, new FixedCardinality(this.Path, 0), this.PathEnd);
            gp.AddTriplePattern(pp);
            return gp;
        }
    }

    public class ZeroOrMorePath : BasePathOperator
    {
        public ZeroOrMorePath(PatternItem start, PatternItem end, ISparqlPath path)
            : base(start, end, path) { }

        public override BaseMultiset Evaluate(SparqlEvaluationContext context)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return "ZeroOrMorePath(" + this.PathStart.ToString() + ", " + this.Path.ToString() + ", " + this.PathEnd.ToString() + ")";
        }

        public override GraphPattern ToGraphPattern()
        {
            GraphPattern gp = new GraphPattern();
            PropertyPathPattern pp = new PropertyPathPattern(this.PathStart, new ZeroOrMore(this.Path), this.PathEnd);
            gp.AddTriplePattern(pp);
            return gp;
        }
    }

    public class OneOrMorePath : BasePathOperator
    {
        public OneOrMorePath(PatternItem start, PatternItem end, ISparqlPath path)
            : base(start, end, path) { }

        public override BaseMultiset Evaluate(SparqlEvaluationContext context)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return "OneOrMorePath(" + this.PathStart.ToString() + ", " + this.Path.ToString() + ", " + this.PathEnd.ToString() + ")";
        }

        public override GraphPattern ToGraphPattern()
        {
            GraphPattern gp = new GraphPattern();
            PropertyPathPattern pp = new PropertyPathPattern(this.PathStart, new OneOrMore(this.Path), this.PathEnd);
            gp.AddTriplePattern(pp);
            return gp;
        }
    }

    public class NegatedPropertySet : ISparqlAlgebra
    {
        private List<INode> _properties = new List<INode>();
        private PatternItem _start, _end;
        private bool _inverse;

        public NegatedPropertySet(PatternItem start, PatternItem end, IEnumerable<Property> properties, bool inverse)
        {
            this._start = start;
            this._end = end;
            this._properties.AddRange(properties.Select(p => p.Predicate));
            this._inverse = inverse;
        }

        public NegatedPropertySet(PatternItem start, PatternItem end, IEnumerable<Property> properties)
            : this(start, end, properties, false) { }

        public PatternItem PathStart
        {
            get
            {
                return this._start;
            }
        }

        public PatternItem PathEnd
        {
            get
            {
                return this._end;
            }
        }

        public IEnumerable<INode> Properties
        {
            get
            {
                return this._properties;
            }
        }

        #region ISparqlAlgebra Members

        public BaseMultiset Evaluate(SparqlEvaluationContext context)
        {
            IEnumerable<Triple> ts;
            String subjVar = this._start.VariableName;
            String objVar = this._end.VariableName;
            if (subjVar != null && context.InputMultiset.ContainsVariable(subjVar))
            {
                if (objVar != null && context.InputMultiset.ContainsVariable(objVar))
                {
                    ts = (from s in context.InputMultiset.Sets
                          where s[subjVar] != null && s[objVar] != null
                          from t in context.Data.GetTriplesWithSubjectObject(s[subjVar], s[objVar])
                          select t);
                }
                else
                {
                    ts = (from s in context.InputMultiset.Sets
                          where s[subjVar] != null
                          from t in context.Data.GetTriplesWithSubject(s[subjVar])
                          select t);
                }
            }
            else if (objVar != null && context.InputMultiset.ContainsVariable(objVar))
            {
                ts = (from s in context.InputMultiset.Sets
                      where s[objVar] != null
                      from t in context.Data.GetTriplesWithObject(s[objVar])
                      select t);
            }
            else
            {
                ts = context.Data.Triples;
            }

            context.OutputMultiset = new Multiset();
            if (this._inverse)
            {
                String temp = objVar;
                objVar = subjVar;
                subjVar = temp;
            }
            foreach (Triple t in ts)
            {
                if (!this._properties.Contains(t.Predicate))
                {
                    Set s = new Set();
                    if (subjVar != null) s.Add(subjVar, t.Subject);
                    if (objVar != null) s.Add(objVar, t.Object);
                    context.OutputMultiset.Add(s);
                }
            }

            if (subjVar == null && objVar == null)
            {
                if (context.OutputMultiset.Count == 0)
                {
                    context.OutputMultiset = new NullMultiset();
                }
                else
                {
                    context.OutputMultiset = new IdentityMultiset();
                }
            }

            return context.OutputMultiset;
        }

        public IEnumerable<string> Variables
        {
            get 
            { 
                throw new NotImplementedException(); 
            }
        }

        public SparqlQuery ToQuery()
        {
            throw new NotImplementedException();
        }

        public GraphPattern ToGraphPattern()
        {
            GraphPattern gp = new GraphPattern();
            PropertyPathPattern pp;
            if (this._inverse)
            {
                pp = new PropertyPathPattern(this.PathStart, new NegatedSet(Enumerable.Empty<Property>(), this._properties.Select(p => new Property(p))), this.PathEnd);
            }
            else
            {
                pp = new PropertyPathPattern(this.PathStart, new NegatedSet(this._properties.Select(p => new Property(p)), Enumerable.Empty<Property>()), this.PathEnd);
            }
            gp.AddTriplePattern(pp);
            return gp;
        }

        #endregion

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            output.Append("NegatedPropertySet(");
            output.Append(this._start.ToString());
            output.Append(", {");
            for (int i = 0; i < this._properties.Count; i++)
            {
                output.Append(this._properties[i].ToString());
                if (i < this._properties.Count - 1)
                {
                    output.Append(", ");
                }
            }
            output.Append("}, ");
            output.Append(this._end.ToString());
            output.Append(')');

            return output.ToString();
        }
    }

}
